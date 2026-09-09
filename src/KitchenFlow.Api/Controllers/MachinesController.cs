using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;

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
    }
}
