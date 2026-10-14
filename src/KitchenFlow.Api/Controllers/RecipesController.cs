using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;
using KitchenFlow.Api.Services;

namespace KitchenFlow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecipesController : ControllerBase
    {
        private readonly KitchenFlowDbContext _context;

        public RecipesController(KitchenFlowDbContext context)
        {
            _context = context;
        }

        // GET /api/recipes : 레시피 목록 (이름 + 단계 수)
        [HttpGet]
        public async Task<IActionResult> GetRecipes()
        {
            var recipes = await _context.Recipes
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    StepCount = r.Steps.Count
                })
                .ToListAsync();

            return Ok(recipes);
        }

        // GET /api/recipes/{id} : 레시피 상세 (단계 목록 + 단계별 재료)
        // Include 대신 Select로 필요한 값만 뽑는다
        //  → 쿼리 1번으로 끝나서 N+1 없음
        //  → Recipe ↔ Step 순환참조로 인한 JSON 에러 없음
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRecipe(int id)
        {
            var recipe = await _context.Recipes
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    Steps = r.Steps
                        .OrderBy(s => s.Order)
                        .Select(s => new
                        {
                            s.Id,
                            s.Order,
                            s.MachineId,
                            MachineName = s.Machine.Name,
                            s.Action,
                            s.DurationMinutes,
                            s.TempC,
                            DependsOn = s.DependsOn.Select(d => d.DependsOnStepId),   // 선행 단계 Id 목록
                            Inputs = s.Inputs.Select(i => new
                            {
                                i.IngredientId,
                                IngredientName = i.Ingredient.Name,
                                i.Ingredient.Unit,
                                i.Quantity
                            })
                        })
                })
                .FirstOrDefaultAsync();

            if (recipe == null)
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            return Ok(recipe);
        }

        // POST /api/recipes/{id}/steps : 단계 추가 (맨 뒤 순서로)
        [HttpPost("{id}/steps")]
        public async Task<IActionResult> AddStep(int id, StepCreateRequest request)
        {
            // 서버 측 입력 검증 (화면에서 막더라도 서버에서 한 번 더)
            if (!await _context.Recipes.AnyAsync(r => r.Id == id))
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }
            if (!await _context.Machines.AnyAsync(m => m.Id == request.MachineId))
            {
                return BadRequest("존재하지 않는 기계입니다.");
            }
            if (string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest("동작을 입력하세요.");
            }
            if (request.DurationMinutes <= 0)
            {
                return BadRequest("소요 시간은 1분 이상이어야 합니다.");
            }

            // 현재 마지막 단계 (없으면 null) → 새 단계는 그 다음 순서
            var lastStep = await _context.Steps
                .Where(s => s.RecipeId == id)
                .OrderByDescending(s => s.Order)
                .FirstOrDefaultAsync();
            int maxOrder = lastStep?.Order ?? 0;

            var step = new Step
            {
                RecipeId = id,
                Order = maxOrder + 1,
                MachineId = request.MachineId,
                Action = request.Action.Trim(),
                DurationMinutes = request.DurationMinutes,
                TempC = request.TempC
            };

            // 기본은 "앞 단계가 끝나면 시작"(순차). 병렬로 하려면 화면에서 선행 단계를 바꾼다.
            if (lastStep != null)
            {
                step.DependsOn.Add(new StepDependency { DependsOnStepId = lastStep.Id });
            }

            _context.Steps.Add(step);
            await _context.SaveChangesAsync();

            return Ok(new { step.Id, step.Order });
        }

        // DELETE /api/recipes/{id}/steps/{stepId} : 단계 삭제 + 뒤 번호 당기기
        // Order를 1,2,3...으로 관리하기로 "알고 선택"했으므로, 삭제 후 전체를 다시 매긴다.
        // (예: 1,2,3,4 에서 2를 지우면 → 1,3,4 가 아니라 1,2,3 이 되도록)
        [HttpDelete("{id}/steps/{stepId}")]
        public async Task<IActionResult> DeleteStep(int id, int stepId)
        {
            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .OrderBy(s => s.Order)
                .ToListAsync();

            var target = steps.FirstOrDefault(s => s.Id == stepId);
            if (target == null)
            {
                return NotFound("해당 단계를 찾을 수 없습니다.");
            }

            // 흐름이 끊기지 않게 "지우는 단계의 선행"을 "지우는 단계를 기다리던 단계"에 이어 준다.
            // (예: 1 → 2 → 3 에서 2를 지우면 1 → 3)
            var dependencies = await _context.StepDependencies
                .Where(d => d.Step.RecipeId == id)
                .ToListAsync();
            var targetPrerequisites = dependencies.Where(d => d.StepId == stepId).Select(d => d.DependsOnStepId).ToList();
            var waitingSteps = dependencies.Where(d => d.DependsOnStepId == stepId).Select(d => d.StepId).ToList();
            foreach (var waitingStepId in waitingSteps)
            {
                foreach (var prerequisiteId in targetPrerequisites)
                {
                    if (!dependencies.Any(d => d.StepId == waitingStepId && d.DependsOnStepId == prerequisiteId))
                    {
                        _context.StepDependencies.Add(new StepDependency { StepId = waitingStepId, DependsOnStepId = prerequisiteId });
                    }
                }
            }

            _context.Steps.Remove(target);   // StepInput, StepDependency는 Cascade로 함께 삭제됨

            // 남은 단계를 1부터 다시 번호 매기기
            int order = 1;
            foreach (var step in steps.Where(s => s.Id != stepId))
            {
                step.Order = order++;
            }

            await _context.SaveChangesAsync();   // 삭제 + 번호 변경을 한 번에 저장
            return NoContent();
        }

        // POST /api/recipes/{id}/steps/{stepId}/move?direction=up|down : 순서 변경
        // 바로 위/아래 단계와 Order를 맞바꾼다. (화면의 ▲▼ 버튼)
        [HttpPost("{id}/steps/{stepId}/move")]
        public async Task<IActionResult> MoveStep(int id, int stepId, [FromQuery] string direction)
        {
            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .OrderBy(s => s.Order)
                .ToListAsync();

            int index = steps.FindIndex(s => s.Id == stepId);
            if (index < 0)
            {
                return NotFound("해당 단계를 찾을 수 없습니다.");
            }

            int swapIndex;
            if (direction == "up")
            {
                swapIndex = index - 1;
            }
            else if (direction == "down")
            {
                swapIndex = index + 1;
            }
            else
            {
                return BadRequest("direction은 up 또는 down이어야 합니다.");
            }

            // 맨 위에서 up, 맨 아래에서 down은 불가
            if (swapIndex < 0 || swapIndex >= steps.Count)
            {
                return BadRequest("더 이상 이동할 수 없습니다.");
            }

            // 두 단계의 Order 맞바꾸기 (튜플 스왑)
            (steps[index].Order, steps[swapIndex].Order) = (steps[swapIndex].Order, steps[index].Order);

            await _context.SaveChangesAsync();   // 두 단계를 한 번에 저장
            return NoContent();
        }

        // POST /api/recipes : 레시피 생성 (화면에서 새 레시피를 만들 때)
        [HttpPost]
        public async Task<IActionResult> CreateRecipe(RecipeCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("레시피 이름을 입력하세요.");
            }

            var recipe = new Recipe { Name = request.Name.Trim() };
            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, new { recipe.Id, recipe.Name });
        }

        // DELETE /api/recipes/{id} : 레시피 삭제
        // 결정(DP2 개념 3): Recipe → Step → StepInput 은 한 묶음이므로 Cascade로 함께 삭제.
        // Ingredient·Machine 은 여러 레시피가 공유하는 별도 존재이므로 삭제되지 않는다(Restrict).
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null)
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/recipes/{id}/steps/{stepId}/dependencies : 선행 단계 지정 (병렬 조리)
        // 본문 예: { "dependsOnStepIds": [4, 6] } → 4, 6이 끝나야 시작. 빈 배열이면 처음부터 바로 시작.
        [HttpPut("{id}/steps/{stepId}/dependencies")]
        public async Task<IActionResult> SetDependencies(int id, int stepId, StepDependenciesRequest request)
        {
            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .Include(s => s.DependsOn)
                .ToListAsync();

            var target = steps.FirstOrDefault(s => s.Id == stepId);
            if (target == null)
            {
                return NotFound("해당 단계를 찾을 수 없습니다.");
            }

            var newIds = request.DependsOnStepIds.Distinct().ToList();
            if (newIds.Contains(stepId))
            {
                return BadRequest("자기 자신을 선행 단계로 지정할 수 없습니다.");
            }
            if (newIds.Any(depId => steps.All(s => s.Id != depId)))
            {
                return BadRequest("같은 레시피의 단계만 선행 단계로 지정할 수 있습니다.");
            }

            // 바꾼 뒤에도 계산이 가능한지(순환이 없는지) 먼저 확인
            var inputs = steps.Select(s => new ScheduleInput(
                s.Id, s.Order, s.MachineId, s.DurationMinutes,
                s.Id == stepId ? newIds : s.DependsOn.Select(d => d.DependsOnStepId).ToList())).ToList();
            if (RecipeScheduler.Compute(inputs) == null)
            {
                return BadRequest("선행 단계가 서로를 기다리는 순환이 생겨서 지정할 수 없습니다.");
            }

            target.DependsOn.RemoveAll(d => !newIds.Contains(d.DependsOnStepId));
            foreach (var depId in newIds.Where(depId => target.DependsOn.All(d => d.DependsOnStepId != depId)))
            {
                target.DependsOn.Add(new StepDependency { StepId = stepId, DependsOnStepId = depId });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST /api/recipes/{id}/validate : 실행 전 검사
        // 위반이 있어도 200 — "검사해 줘"라는 요청 자체는 성공했고, 위반 목록이 그 결과다.
        // 404는 레시피가 없을 때(요청 자체가 잘못됐을 때)만.
        [HttpPost("{id}/validate")]
        public async Task<IActionResult> ValidateRecipe(int id)
        {
            if (!await _context.Recipes.AnyAsync(r => r.Id == id))
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            // ① DB → 검증기 입력으로 옮겨 담기 (DB를 만지는 건 여기까지)
            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .OrderBy(s => s.Order)
                .Select(s => new ValidationStep(
                    s.Id,
                    s.Order,
                    s.MachineId,
                    s.DurationMinutes,
                    s.TempC,
                    s.Inputs.Select(i => new ValidationInput(i.IngredientId, i.Quantity)).ToList()))
                .ToListAsync();

            var machines = await _context.Machines.ToDictionaryAsync(
                m => m.Id,
                m => new ValidationMachine(m.Id, m.Name, m.CapacityMl, m.MinTempC, m.MaxTempC, m.IsAvailable));

            var ingredients = await _context.Ingredients.ToDictionaryAsync(
                i => i.Id,
                i => new ValidationIngredient(i.Id, i.Name, i.Unit, i.StockQty));

            // ② 판단은 순수 함수에게
            var violations = RecipeValidator.Validate(steps, machines, ingredients);

            return Ok(new
            {
                RecipeId = id,
                IsValid = violations.Count == 0,
                Violations = violations
            });
        }
        // POST /api/recipes/{id}/simulate : 순차 실행 시뮬레이션 (T6)
        // 실제로 돌리지 않고 "언제 무엇이 일어나는지"만 한 번에 계산해서 돌려준다. (DP4 결정 B)
        [HttpPost("{id}/simulate")]
        public async Task<IActionResult> SimulateRecipe(int id)
        {
            if (!await _context.Recipes.AnyAsync(r => r.Id == id))
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            // ① DB → 계산 함수 입력으로 옮겨 담기
            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .Select(s => new SimulationStep(s.Id, s.Order, s.DurationMinutes))
                .ToListAsync();

            // ② 계산은 순수 함수에게
            var timeline = RecipeSimulator.Simulate(steps);

            return Ok(timeline);
        }

        // GET /api/recipes/{id}/schedule : 조리 흐름 계산
        // 여러 단계를 동시에 진행해서 한 요리로 완성되기까지의 시작/종료 시각
        [HttpGet("{id}/schedule")]
        public async Task<IActionResult> GetSchedule(int id)
        {
            if (!await _context.Recipes.AnyAsync(r => r.Id == id))
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            var steps = await _context.Steps
                .Where(s => s.RecipeId == id)
                .Select(s => new
                {
                    s.Id,
                    s.Order,
                    s.MachineId,
                    MachineName = s.Machine.Name,
                    s.Action,
                    s.DurationMinutes,
                    DependsOn = s.DependsOn.Select(d => d.DependsOnStepId).ToList()
                })
                .ToListAsync();

            var scheduled = RecipeScheduler.Compute(
                steps.Select(s => new ScheduleInput(s.Id, s.Order, s.MachineId, s.DurationMinutes, s.DependsOn)).ToList());
            if (scheduled == null)
            {
                return BadRequest("선행 단계에 순환이 있어 조리 흐름을 계산할 수 없습니다.");
            }

            var timeOf = scheduled.ToDictionary(x => x.StepId);
            var result = steps
                .OrderBy(s => timeOf[s.Id].StartMinute).ThenBy(s => s.Order)
                .Select(s => new
                {
                    s.Id,
                    s.Order,
                    s.Action,
                    s.MachineId,
                    s.MachineName,
                    s.DurationMinutes,
                    s.DependsOn,
                    timeOf[s.Id].StartMinute,
                    timeOf[s.Id].EndMinute
                })
                .ToList();

            return Ok(new
            {
                RecipeId = id,
                TotalMinutes = scheduled.Count == 0 ? 0 : scheduled.Max(x => x.EndMinute),   // 동시에 진행했을 때 완성까지
                SequentialMinutes = steps.Sum(s => s.DurationMinutes),                      // 하나씩 순서대로 했을 때
                Steps = result
            });
        }

        // GET /api/recipes/combined-schedule?recipeIds=2&recipeIds=3
        // 손님 주문(여러 요리)을 한꺼번에 만들 때의 조리 흐름
        //  - 여러 요리의 단계를 한 번에 계산 → 같은 기계를 쓰는 단계는 서로 기다린다 (기계 경합)
        //  - 모든 요리가 다 되는 시각에 한곳에 모아 손님께 나간다 (서빙 합류)
        [HttpGet("combined-schedule")]
        public async Task<IActionResult> GetCombinedSchedule([FromQuery] List<int> recipeIds)
        {
            var ids = recipeIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return BadRequest("요리를 하나 이상 선택하세요.");
            }

            var recipes = await _context.Recipes
                .Where(r => ids.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToListAsync();
            if (recipes.Count != ids.Count)
            {
                return NotFound("존재하지 않는 레시피가 있습니다.");
            }

            var steps = await _context.Steps
                .Where(s => ids.Contains(s.RecipeId))
                .Select(s => new
                {
                    s.Id,
                    s.RecipeId,
                    s.Order,
                    s.MachineId,
                    MachineName = s.Machine.Name,
                    s.Action,
                    s.DurationMinutes,
                    DependsOn = s.DependsOn.Select(d => d.DependsOnStepId).ToList()
                })
                .ToListAsync();

            // 모든 요리의 단계를 한 번에 넣는다 → 기계가 겹치면 스케줄러가 알아서 기다리게 한다
            var scheduled = RecipeScheduler.Compute(
                steps.Select(s => new ScheduleInput(s.Id, s.Order, s.MachineId, s.DurationMinutes, s.DependsOn)).ToList());
            if (scheduled == null)
            {
                return BadRequest("선행 단계에 순환이 있어 조리 흐름을 계산할 수 없습니다.");
            }
            var timeOf = scheduled.ToDictionary(x => x.StepId);

            // 서빙 시각 = 가장 늦게 끝나는 요리의 완료 시각
            int serveMinute = scheduled.Count == 0 ? 0 : scheduled.Max(x => x.EndMinute);

            var dishes = recipes.Select(r =>
            {
                var myEnds = steps.Where(s => s.RecipeId == r.Id).Select(s => timeOf[s.Id].EndMinute).ToList();
                int finish = myEnds.Count == 0 ? 0 : myEnds.Max();
                return new
                {
                    RecipeId = r.Id,
                    r.Name,
                    FinishMinute = finish,                        // 이 요리가 다 되는 시각
                    WaitBeforeServe = serveMinute - finish        // 서빙까지 기다리는(식는) 시간
                };
            }).ToList();

            return Ok(new
            {
                ServeMinute = serveMinute,
                SequentialMinutes = steps.Sum(s => s.DurationMinutes),   // 한 요리씩, 한 단계씩 했을 때
                Dishes = dishes,
                Steps = steps
                    .OrderBy(s => timeOf[s.Id].StartMinute).ThenBy(s => s.RecipeId)
                    .Select(s => new
                    {
                        s.Id,
                        s.RecipeId,
                        s.Order,
                        s.Action,
                        s.MachineId,
                        s.MachineName,
                        timeOf[s.Id].StartMinute,
                        timeOf[s.Id].EndMinute
                    })
            });
        }
    }

    // 선행 단계 지정 요청 본문
    public class StepDependenciesRequest
    {
        public List<int> DependsOnStepIds { get; set; } = new();
    }

    // 단계 추가 요청 본문
    public class StepCreateRequest
    {
        public int MachineId { get; set; }
        public string Action { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int? TempC { get; set; }
    }

    // 레시피 생성 요청 본문
    public class RecipeCreateRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
