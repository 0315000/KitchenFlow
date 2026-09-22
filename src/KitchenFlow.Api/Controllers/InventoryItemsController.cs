using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryItemsController : ControllerBase
    {
        private readonly KitchenFlowDbContext _context;

        public InventoryItemsController(KitchenFlowDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetInventoryItems()
        {
            var items = await _context.InventoryItems.ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound("해당 재료를 찾을 수 없습니다.");
            }
            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInventoryItem(InventoryItem item)
        {
            var error = ValidateCommon(item);
            if (error == null && item.ExpiryDate.Date < DateTime.Today)
            {
                // DP3-1: 유통기한은 필수 입력이며 과거 날짜로 "신규 등록"은 불가.
                // (필드를 아예 안 보내면 DateTime 기본값 0001-01-01이 되어 이 조건에 걸리므로
                //  누락된 경우도 함께 걸러진다.)
                error = "유통기한(ExpiryDate)은 필수이며 과거 날짜로 등록할 수 없습니다.";
            }
            if (error != null)
            {
                return BadRequest(error);
            }

            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInventoryItem(int id, InventoryItem item)
        {
            if (id != item.Id)
            {
                return BadRequest("URL의 id와 요청 본문의 id가 일치하지 않습니다.");
            }

            var existing = await _context.InventoryItems.FindAsync(id);
            if (existing == null)
            {
                return NotFound("해당 재료를 찾을 수 없습니다.");
            }

            // 수정 시에는 "신규 등록" 규칙(과거 날짜 금지)을 강제하지 않는다.
            // 이미 유통기한이 지난 재고를 폐기 처리 전 수량만 고치는 등의 정상적인 수정까지
            // 막아버리면 안 되기 때문이다. ItemName/Quantity/Threshold 기본 검증은 동일하게 적용한다.
            var error = ValidateCommon(item);
            if (error != null)
            {
                return BadRequest(error);
            }

            existing.ItemName = item.ItemName;
            existing.Quantity = item.Quantity;
            existing.ExpiryDate = item.ExpiryDate;
            existing.Threshold = item.Threshold;
            existing.Unit = item.Unit;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound("해당 재료를 찾을 수 없습니다.");
            }

            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DP3-2 동시성 요구사항: 여러 조리 작업이 거의 동시에 같은 재료를 차감 요청해도
        // 재고가 음수가 되어서는 안 된다.
        //
        // "조회 후 저장(check-then-act)" 방식은 MachinesController의 이름 중복 검사에서 봤듯이
        // 두 요청이 동시에 조회를 통과해버릴 수 있어 안전하지 않다. 대신 EF Core의
        // ExecuteUpdateAsync로 "Quantity >= 차감량인 경우에만 차감"하는 조건부 UPDATE 문 한 줄을
        // DB에 직접 실행한다. 이 UPDATE는 DB 엔진이 원자적으로 처리하므로, 두 요청이 동시에
        // 들어와도 실제로는 하나씩 순서대로 반영되고, 재고보다 많이 차감하려는 요청은
        // WHERE 조건에서 걸러져 0건 반영(=실패)으로 확인할 수 있다.
        [HttpPost("{id}/consume")]
        public async Task<IActionResult> ConsumeInventoryItem(int id, ConsumeInventoryRequest request)
        {
            if (request == null || request.Amount <= 0)
            {
                return BadRequest("차감 수량(Amount)은 0보다 커야 합니다.");
            }

            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound("해당 재료를 찾을 수 없습니다.");
            }

            if (item.ExpiryDate.Date < DateTime.Today)
            {
                // DP2-1/DP3-2: 유통기한이 지난 재료는 조리 작업에서 사용을 차단한다.
                return BadRequest("유통기한이 지난 재료는 사용할 수 없습니다.");
            }

            var affectedRows = await _context.InventoryItems
                .Where(i => i.Id == id && i.Quantity >= request.Amount)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Quantity, i => i.Quantity - request.Amount));

            if (affectedRows == 0)
            {
                // 위 FindAsync 시점 이후 다른 요청이 먼저 차감했을 수도 있고,
                // 애초에 재고가 부족했을 수도 있다. 어느 쪽이든 "재고 부족"으로 명확히 안내한다.
                return BadRequest("재고가 부족하여 요청한 수량을 차감할 수 없습니다.");
            }

            return NoContent();
        }

                [HttpPost("{id}/adjust")]
        public async Task<IActionResult> AdjustInventoryItem(int id, AdjustInventoryRequest request)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null)
            {
                return NotFound("해당 재료를 찾을 수 없습니다.");
            }

            var affectedRows = await _context.InventoryItems
                .Where(i => i.Id == id && i.Quantity + request.Amount >= 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Quantity, i => i.Quantity + request.Amount));

            if (affectedRows == 0)
            {
                return BadRequest("이 조정을 적용하면 재고가 음수가 됩니다.");
            }

            return NoContent();
        }
        private static string? ValidateCommon(InventoryItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ItemName))
            {
                return "재료명(ItemName)은 비어 있을 수 없습니다.";
            }

            if (item.ItemName.Length > 100)
            {
                return "재료명(ItemName)은 100자를 초과할 수 없습니다.";
            }

            if (item.Quantity < 0)
            {
                return "수량(Quantity)은 0 이상이어야 합니다.";
            }

            // Threshold(최소 재고 임계치) 자체는 DP3-1에 별도 검증 규칙이 명시되어 있지 않지만,
            // 음수 임계치는 "재고 부족" 판정 로직을 무의미하게 만들므로 방어적으로 막아둔다.
            if (item.Threshold < 0)
            {
                return "최소 재고 임계치(Threshold)는 0 이상이어야 합니다.";
            }

            return null;
        }
    }
}
