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
                run.Succeeded = false;
                run.FailureReason = "선행 단계에 순환이 있어 실행할 수 없습니다.";
            }
            else
            {
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
            }

            _context.Runs.Add(run);
            await _context.SaveChangesAsync();   // Run + RunEvent 들을 한 번에 저장
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