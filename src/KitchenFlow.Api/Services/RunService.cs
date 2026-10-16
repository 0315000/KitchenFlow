using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Services
{
    // T9: 레시피 실행 → 기록 저장 + 재고 차감.
    // 재고 차감은 API 여기저기에 흩뿌리지 않고 이 서비스 한 곳에서만 한다.
    public class RunService
    {
        private readonly KitchenFlowDbContext _context;

        public RunService(KitchenFlowDbContext context)
        {
            _context = context;
        }

        // 레시피가 없으면 null
        public async Task<Run?> RunAsync(int recipeId)
        {
            // ① 레시피를 한 번만 읽는다 — 이후 단계는 DB를 다시 읽지 않고 이 값만 쓴다.
            //    (검증과 시뮬레이션 사이에 레시피가 편집돼도 둘이 같은 레시피를 보게 하려고)
            var recipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.Steps).ThenInclude(s => s.Inputs)
                .Include(r => r.Steps).ThenInclude(s => s.DependsOn)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return null;
            }

            var run = new Run
            {
                RecipeId = recipe.Id,
                StartedAt = DateTime.UtcNow,
                RecipeSnapshot = TakeSnapshot(recipe)
            };

            // ③ 시뮬레이션 — /simulate 와 같은 계산 (T8 기계 경합 포함)
            var steps = recipe.Steps
                .Select(s => new SimulationStep(
                    s.MachineId,
                    s.Id,
                    s.Order,
                    s.DurationMinutes,
                    s.DependsOn.Select(d => d.DependsOnStepId).ToList()))
                .ToList();
            
            var timeline = RecipeSimulator.Simulate(steps);
            if (timeline == null)
            {
                return await SaveFailedAsync(run, "선행 단계에 순환이 있어 실행할 수 없습니다.");
            }

            // 타임라인 한 칸 = RunEvent 한 줄
            run.Succeeded = true;
            run.Events = timeline
                .Select(t => new RunEvent
                {
                    StepId = t.StepId,
                    MachineId = t.MachineId,
                    StartMin = t.StartMin,
                    EndMin = t.EndMin
                                })
                .ToList();

            // ④ 재료별 필요량 합계 — 같은 재료를 여러 단계에서 쓰면 더한다 (예: 로스트치킨 버터 50 + 30)
            var needs = recipe.Steps
                .SelectMany(s => s.Inputs)
                .GroupBy(i => i.IngredientId)
                .Select(g => new { IngredientId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                                .ToList();

            // ⑤ 재고 차감 + 성공 기록을 한 트랜잭션으로 — 전부 되거나, 하나도 안 되거나
            string? shortage = null;
            await using (var tx = await _context.Database.BeginTransactionAsync())
            {
                foreach (var need in needs)
                {
                    // 조건부 UPDATE 한 줄: 재고가 충분할 때만 깎는다 (T3 consume 과 같은 방식)
                    // 확인과 차감이 한 문장이라, 그 사이에 다른 실행이 끼어들 틈이 없다.
                    var updated = await _context.Ingredients
                        .Where(i => i.Id == need.IngredientId && i.StockQty >= need.Quantity)
                        .ExecuteUpdateAsync(set => set.SetProperty(i => i.StockQty, i => i.StockQty - need.Quantity));

                    if (updated == 0)
                    {
                        shortage = $"재료(Id {need.IngredientId}) 재고 부족 — 필요 {need.Quantity}";
                        break;
                    }
                }
                

                if (shortage == null)
                {
                    _context.Runs.Add(run);
                    await _context.SaveChangesAsync();   // Run + RunEvent 들을 저장
                    await tx.CommitAsync();              // 차감 + 기록을 한꺼번에 확정
                    return run;
                }

                await tx.RollbackAsync();   // 앞에서 이미 깎은 재료까지 전부 되돌린다
            }

            // ⑥ 실패 기록은 트랜잭션 밖에서 — 안에서 저장하면 롤백할 때 기록까지 같이 사라진다
            return await SaveFailedAsync(run, shortage);
        }

        // 할 일 3: 레시피의 실행 이력 (최신순). 레시피가 없으면 null
        public async Task<List<Run>?> GetRunsAsync(int recipeId)
        {
            if (!await _context.Recipes.AnyAsync(r => r.Id == recipeId))
            {
                return null;
            }

            return await _context.Runs
                .AsNoTracking()
                .Include(r => r.Events)               // 총 소요시간 계산용
                .Where(r => r.RecipeId == recipeId)
                .OrderByDescending(r => r.Id)         // 최신 실행이 위로
                .ToListAsync();
        }

        // 실패도 Run 으로 남긴다 (완료 조건: "실패로 기록되고 재고는 그대로")
        private async Task<Run> SaveFailedAsync(Run run, string reason)
        {
            run.Succeeded = false;
            run.FailureReason = reason;
            run.Events.Clear();   // 실제로 실행되지 않았으니 타임라인은 남기지 않는다
            _context.Runs.Add(run);
            await _context.SaveChangesAsync();
            return run;
        }

        // 실행 시점의 레시피를 JSON 으로 복사해 둔다.
        // 나중에 레시피가 편집/삭제돼도 이 Run 은 그때 모습으로 재생할 수 있다.
        private static string TakeSnapshot(Recipe recipe)
        {
            var snapshot = new
            {
                recipe.Id,
                recipe.Name,
                Steps = recipe.Steps
                    .OrderBy(s => s.Order)
                    .Select(s => new
                    {
                        s.Id,
                        s.Order,
                        s.MachineId,
                        s.Action,
                        s.DurationMinutes,
                        s.TempC,
                        Inputs = s.Inputs.Select(i => new { i.IngredientId, i.Quantity }),
                        DependsOn = s.DependsOn.Select(d => d.DependsOnStepId)
                    })
            };
            return JsonSerializer.Serialize(snapshot);
        }
    }
}