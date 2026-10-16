using Microsoft.AspNetCore.Mvc;
using KitchenFlow.Api.Services;

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
                Events = run.Events.Select(e => new { e.StepId, e.MachineId, e.StartMin,e.EndMin })
            });
        }
    }
}