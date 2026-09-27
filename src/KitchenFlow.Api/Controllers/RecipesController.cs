using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;

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

            // 현재 마지막 순서 + 1 (단계가 하나도 없으면 1)
            int maxOrder = await _context.Steps
                .Where(s => s.RecipeId == id)
                .MaxAsync(s => (int?)s.Order) ?? 0;

            var step = new Step
            {
                RecipeId = id,
                Order = maxOrder + 1,
                MachineId = request.MachineId,
                Action = request.Action.Trim(),
                DurationMinutes = request.DurationMinutes,
                TempC = request.TempC
            };

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

            _context.Steps.Remove(target);   // StepInput은 Cascade로 함께 삭제됨

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
