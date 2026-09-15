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
            await _context.SaveChangesAsync();

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

            await _context.SaveChangesAsync();
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


