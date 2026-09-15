using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CookingTasksController : ControllerBase
    {
        private readonly KitchenFlowDbContext _context;

        public CookingTasksController(KitchenFlowDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCookingTasks()
        {
            var tasks = await _context.CookingTasks.ToListAsync();
            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCookingTask(int id)
        {
            var task = await _context.CookingTasks.FindAsync(id);
            if (task == null)
            {
                return NotFound("해당 작업을 찾을 수 없습니다.");
            }
            return Ok(task);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCookingTask(CookingTask task)
        {
            var error = await ValidateAsync(task);
            if (error != null)
            {
                return BadRequest(error);
            }

            _context.CookingTasks.Add(task);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest("작업을 저장하는 중 문제가 발생했습니다. 입력값을 다시 확인해주세요.");
            }

            return CreatedAtAction(nameof(GetCookingTask), new { id = task.Id }, task);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCookingTask(int id, CookingTask task)
        {
            if (id != task.Id)
            {
                return BadRequest("URL의 id와 요청 본문의 id가 일치하지 않습니다.");
            }

            var existing = await _context.CookingTasks.FindAsync(id);
            if (existing == null)
            {
                return NotFound("해당 작업을 찾을 수 없습니다.");
            }

            var error = await ValidateAsync(task, excludingId: id);
            if (error != null)
            {
                return BadRequest(error);
            }

            existing.Name = task.Name;
            existing.RequiredMachineId = task.RequiredMachineId;
            existing.DurationMinutes = task.DurationMinutes;
            existing.PrecedenceTaskId = task.PrecedenceTaskId;
            existing.RequiredIngredientId = task.RequiredIngredientId;
            existing.RequiredIngredientAmount = task.RequiredIngredientAmount;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest("작업을 저장하는 중 문제가 발생했습니다. 입력값을 다시 확인해주세요.");
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCookingTask(int id)
        {
            var task = await _context.CookingTasks.FindAsync(id);
            if (task == null)
            {
                return NotFound("해당 작업을 찾을 수 없습니다.");
            }

            _context.CookingTasks.Remove(task);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 다른 작업이 이 작업을 PrecedenceTaskId로 참조 중이면 DB의 Restrict 제약에 걸린다.
                return BadRequest("다른 작업이 이 작업을 선행 작업으로 참조하고 있어 삭제할 수 없습니다.");
            }

            return NoContent();
        }

        // DP3-1/DP3-2: 여러 작업을 등록 순서(Id) 기준으로 위상 정렬하면서,
        // 같은 기계를 요구하는 작업들은 그 기계가 비는 시점에 순차 배정한다.
        // 매 요청마다 새로 계산하기 때문에 별도의 "재계산" 로직이 필요 없다
        // (기계가 고장/점검 중이면 그 시점의 계산에서 자동으로 반영됨).
        [HttpGet("schedule")]
        public async Task<IActionResult> GetSchedule()
        {
            var tasks = await _context.CookingTasks.OrderBy(t => t.Id).ToListAsync();
            var machines = await _context.Machines.ToDictionaryAsync(m => m.Id);
            var ingredients = await _context.InventoryItems.ToDictionaryAsync(i => i.Id);
            var byId = tasks.ToDictionary(t => t.Id);

            var scheduled = new Dictionary<int, (int Start, int End)>();
            var blocked = new HashSet<int>();
            var machineFreeAt = new Dictionary<int, int>();

            var remaining = new List<CookingTask>(tasks);
            bool progressed = true;
            while (remaining.Count > 0 && progressed)
            {
                progressed = false;
                foreach (var task in remaining.ToList())
                {
                    bool precedenceUnresolved = task.PrecedenceTaskId.HasValue
                        && byId.ContainsKey(task.PrecedenceTaskId.Value)
                        && !scheduled.ContainsKey(task.PrecedenceTaskId.Value)
                        && !blocked.Contains(task.PrecedenceTaskId.Value);

                    if (precedenceUnresolved)
                    {
                        continue; // 선행 작업이 아직 처리 전 -> 다음 라운드에 다시 시도
                    }

                    remaining.Remove(task);
                    progressed = true;

                    machines.TryGetValue(task.RequiredMachineId, out var machine);
                    bool machineAvailable = machine != null && machine.IsAvailable;
                    bool precedenceBlocked = task.PrecedenceTaskId.HasValue
                        && blocked.Contains(task.PrecedenceTaskId.Value);

                    if (!machineAvailable || precedenceBlocked)
                    {
                        // 기계 사용 불가, 또는 선행 작업 자체가 배정 불가 -> 이 작업도 배정 불가
                        blocked.Add(task.Id);
                        continue;
                    }

                    int earliestStart = task.PrecedenceTaskId.HasValue
                        && scheduled.TryGetValue(task.PrecedenceTaskId.Value, out var prev)
                        ? prev.End
                        : 0;

                    machineFreeAt.TryGetValue(task.RequiredMachineId, out var freeAt);
                    int start = Math.Max(earliestStart, freeAt);
                    int end = start + task.DurationMinutes;

                    scheduled[task.Id] = (start, end);
                    machineFreeAt[task.RequiredMachineId] = end;
                }
            }

            // 여기까지 왔는데 remaining에 남아있다면 감지되지 않은 순환 의존성(비정상 상태) ->
            // 안전하게 전부 배정 불가로 처리한다 (크래시 대신).
            foreach (var leftover in remaining)
            {
                blocked.Add(leftover.Id);
            }

            var result = tasks.Select(t =>
            {
                machines.TryGetValue(t.RequiredMachineId, out var machine);

                bool ingredientShortage = false;
                if (t.RequiredIngredientId.HasValue && t.RequiredIngredientAmount.HasValue
                    && ingredients.TryGetValue(t.RequiredIngredientId.Value, out var ingredient))
                {
                    ingredientShortage = ingredient.Quantity < t.RequiredIngredientAmount.Value;
                }

                scheduled.TryGetValue(t.Id, out var window);
                bool isScheduled = scheduled.ContainsKey(t.Id);

                int earliestPossible = t.PrecedenceTaskId.HasValue
                    && scheduled.TryGetValue(t.PrecedenceTaskId.Value, out var p)
                    ? p.End
                    : 0;
                int waitMinutes = isScheduled ? Math.Max(0, window.Start - earliestPossible) : 0;

                return new ScheduledCookingTask
                {
                    TaskId = t.Id,
                    Name = t.Name,
                    RequiredMachineId = t.RequiredMachineId,
                    MachineName = machine?.Name ?? "(알 수 없음)",
                    DurationMinutes = t.DurationMinutes,
                    PrecedenceTaskId = t.PrecedenceTaskId,
                    MachineAvailable = machine?.IsAvailable ?? false,
                    StartMinute = isScheduled ? window.Start : null,
                    EndMinute = isScheduled ? window.End : null,
                    WaitMinutes = waitMinutes,
                    IngredientShortage = ingredientShortage
                };
            }).ToList();

            return Ok(result);
        }

        private async Task<string?> ValidateAsync(CookingTask task, int? excludingId = null)
        {
            if (string.IsNullOrWhiteSpace(task.Name))
            {
                return "작업명(Name)은 비어 있을 수 없습니다.";
            }

            if (task.Name.Length > 100)
            {
                return "작업명(Name)은 100자를 초과할 수 없습니다.";
            }

            if (task.DurationMinutes <= 0)
            {
                return "소요 시간(DurationMinutes)은 0보다 큰 정수여야 합니다.";
            }

            var machineExists = await _context.Machines.AnyAsync(m => m.Id == task.RequiredMachineId);
            if (!machineExists)
            {
                return "존재하지 않는 기계(RequiredMachineId)입니다.";
            }

            if (task.PrecedenceTaskId.HasValue)
            {
                if (excludingId.HasValue && task.PrecedenceTaskId.Value == excludingId.Value)
                {
                    return "자기 자신을 선행 작업(PrecedenceTaskId)으로 지정할 수 없습니다.";
                }

                var precedenceExists = await _context.CookingTasks.AnyAsync(t => t.Id == task.PrecedenceTaskId.Value);
                if (!precedenceExists)
                {
                    return "존재하지 않는 선행 작업(PrecedenceTaskId)입니다.";
                }

                // 신규 등록(excludingId == null)일 때는 0을 넘긴다. 아직 저장되지 않은 새 작업의 id는
                // 기존 어떤 작업의 체인에도 등장할 수 없으므로, 사이클이 원천적으로 불가능하다.
                var taskIdForCycleCheck = excludingId ?? 0;
                if (await WouldCreateCycleAsync(taskIdForCycleCheck, task.PrecedenceTaskId))
                {
                    return "순환 의존성이 감지되었습니다.";
                }
            }

            if (task.RequiredIngredientId.HasValue)
            {
                var ingredientExists = await _context.InventoryItems.AnyAsync(i => i.Id == task.RequiredIngredientId.Value);
                if (!ingredientExists)
                {
                    return "존재하지 않는 재료(RequiredIngredientId)입니다.";
                }

                if (!task.RequiredIngredientAmount.HasValue || task.RequiredIngredientAmount.Value <= 0)
                {
                    return "필요 재료 수량(RequiredIngredientAmount)은 0보다 커야 합니다.";
                }
            }

            return null;
        }

        // taskId를 새 선행 작업(newPrecedenceTaskId)에 연결했을 때, 그 체인을 따라가다
        // taskId 자신으로 되돌아오는지 검사한다. (PrecedenceTaskId는 노드당 화살표 1개뿐이라
        // 단순 연결 리스트 형태이므로, 이렇게만 검사해도 모든 순환을 잡아낼 수 있다.)
        private async Task<bool> WouldCreateCycleAsync(int taskId, int? newPrecedenceTaskId)
        {
            if (newPrecedenceTaskId == null)
            {
                return false;
            }

            var precedenceMap = await _context.CookingTasks
                .Select(t => new { t.Id, t.PrecedenceTaskId })
                .ToDictionaryAsync(t => t.Id, t => t.PrecedenceTaskId);

            int? current = newPrecedenceTaskId;
            var visited = new HashSet<int>();
            while (current.HasValue)
            {
                if (current.Value == taskId)
                {
                    return true;
                }
                if (!visited.Add(current.Value))
                {
                    break; // 이 체인에 이미 다른 순환이 있는 비정상 상태 -> 무한루프 방지용 탈출
                }
                precedenceMap.TryGetValue(current.Value, out var next);
                current = next;
            }
            return false;
        }
    }
}
