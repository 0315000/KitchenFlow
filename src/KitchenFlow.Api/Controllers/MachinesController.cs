using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MachinesController : ControllerBase
    {
        private readonly KitchenFlowDbContext _context;

        public MachinesController(KitchenFlowDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMachines()
        {
            var machines = await _context.Machines.ToListAsync();
            return Ok(machines);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMachine(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
            {
                return NotFound("해당 기계를 찾을 수 없습니다.");
            }
            return Ok(machine);
        }

        [HttpPost]
        public async Task<IActionResult> CreateMachine(Machine machine)
        {
            var error = await ValidateAsync(machine);
            if (error != null)
            {
                return BadRequest(error);
            }

            _context.Machines.Add(machine);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 애플리케이션 레벨 중복 검사를 통과했더라도, 거의 동시에 들어온 다른 요청이
                // 먼저 저장되어 DB의 고유 인덱스(Name)에 걸릴 수 있다(몽키테스트/동시성 대비).
                // 이 경우 500 대신 400으로 명확한 사유를 반환한다.
                return BadRequest("이미 같은 이름의 기계가 존재합니다.");
            }

            return CreatedAtAction(nameof(GetMachine), new { id = machine.Id }, machine);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMachine(int id, Machine machine)
        {
            if (id != machine.Id)
            {
                return BadRequest("URL의 id와 요청 본문의 id가 일치하지 않습니다.");
            }

            var existing = await _context.Machines.FindAsync(id);
            if (existing == null)
            {
                return NotFound("해당 기계를 찾을 수 없습니다.");
            }

            var error = await ValidateAsync(machine, excludingId: id);
            if (error != null)
            {
                return BadRequest(error);
            }

            existing.Name = machine.Name;
            existing.Kind = machine.Kind;
            existing.CapacityMl = machine.CapacityMl;
            existing.MinTempC = machine.MinTempC;
            existing.MaxTempC = machine.MaxTempC;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // CreateMachine과 동일한 이유: 동시 요청 경합 시 DB 고유 인덱스가 최종 방어선 역할을 한다.
                return BadRequest("이미 같은 이름의 기계가 존재합니다.");
            }
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMachine(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
            {
                return NotFound("해당 기계를 찾을 수 없습니다.");
            }

            _context.Machines.Remove(machine);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static readonly string[] AllowedKinds = { "Oven", "Mixer", "Fridge" };

        private async Task<string?> ValidateAsync(Machine machine, int? excludingId = null)
        {
            if (string.IsNullOrWhiteSpace(machine.Name))
            {
                return "이름(Name)은 비어 있을 수 없습니다.";
            }

            if (machine.Name.Trim().Length != machine.Name.Length)
            {
                return "이름(Name)의 앞뒤에 공백을 포함할 수 없습니다.";
            }

            if (machine.Name.Length > 100)
            {
                return "이름(Name)은 100자를 초과할 수 없습니다.";
            }

            if (string.IsNullOrWhiteSpace(machine.Kind) || !AllowedKinds.Contains(machine.Kind))
            {
                return $"종류(Kind)는 다음 값 중 하나여야 합니다: {string.Join(", ", AllowedKinds)}";
            }

            if (machine.CapacityMl <= 0)
            {
                return "용량(CapacityMl)은 0보다 큰 정수여야 합니다.";
            }

            if (machine.MinTempC > machine.MaxTempC)
            {
                return "최저온도(MinTempC)는 최고온도(MaxTempC)보다 클 수 없습니다.";
            }

            var duplicateQuery = _context.Machines.Where(m => m.Name == machine.Name);
            if (excludingId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(m => m.Id != excludingId.Value);
            }
            if (await duplicateQuery.AnyAsync())
            {
                return "이미 같은 이름의 기계가 존재합니다.";
            }

            return null;
        }
    }
}


