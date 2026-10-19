using Microsoft.AspNetCore.Mvc;
using KitchenFlow.Api.Services;
using System.Text.Json;

namespace KitchenFlow.Api.Controllers
{
    // T9: 레시피 실행 기록
    [ApiController]
    [Route("api/recipes/{recipeId}/runs")]
    public class RunsController : ControllerBase
    {
        private readonly RunService _runService;

        public RunsController(RunService runService)
        {
            _runService = runService;
        }

        // POST /api/recipes/{recipeId}/runs : 레시피 실행
        [HttpPost]
        public async Task<IActionResult> CreateRun(int recipeId)
        {
            var run = await _runService.RunAsync(recipeId);
            if (run == null)
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            // 새 실행 기록을 만들었으므로 201. (실패도 "기록은 만들어졌다" → 201, 결과는 Succeeded 로 구분)
            return StatusCode(StatusCodes.Status201Created, new
            {
                run.Id,
                run.RecipeId,
                run.StartedAt,
                run.Succeeded,
                run.FailureReason,
                Events = run.Events.Select(e => new { e.StepId, e.MachineId, e.StartMin, e.EndMin, e.IsPreheat })
            });
        }

        // GET /api/recipes/{recipeId}/runs : 실행 이력 목록 (최신순)
        [HttpGet]
        public async Task<IActionResult> GetRuns(int recipeId)
        {
            var runs = await _runService.GetRunsAsync(recipeId);
            if (runs == null)
            {
                return NotFound("해당 레시피를 찾을 수 없습니다.");
            }

            return Ok(runs.Select(r => new
            {
                r.Id,
                r.StartedAt,
                r.Succeeded,
                r.FailureReason,
                // 총 소요시간 = 가장 늦게 끝난 단계의 끝 시각 (실패한 실행은 이벤트가 없어 0)
                TotalMinutes = r.Events.Count == 0 ? 0 : r.Events.Max(e => e.EndMin)
            }));
        }

        // GET /api/recipes/{recipeId}/runs/{runId} : 실행 1건 상세 (타임라인 재생 + 스코어 + 실패 사유용)
        [HttpGet("{runId}")]
        public async Task<IActionResult> GetRun(int recipeId, int runId)
        {
            var run = await _runService.GetRunAsync(recipeId, runId);
            if (run == null)
            {
                return NotFound("해당 실행 기록을 찾을 수 없습니다.");
            }

            return Ok(new
            {
                run.Id,
                run.RecipeId,
                run.StartedAt,
                run.Succeeded,
                run.FailureReason,
                TotalMinutes = run.Events.Count == 0 ? 0 : run.Events.Max(e => e.EndMin),
                Events = run.Events
                    .OrderBy(e => e.StartMin)
                    .Select(e => new { e.StepId, e.MachineId, e.StartMin, e.EndMin, e.IsPreheat }),
                // DB에는 JSON "글자"로 저장돼 있어서, 객체로 바꿔 보내야 화면에서 바로 쓸 수 있다
                RecipeSnapshot = JsonSerializer.Deserialize<JsonElement>(run.RecipeSnapshot)
            });
        }
    }
}