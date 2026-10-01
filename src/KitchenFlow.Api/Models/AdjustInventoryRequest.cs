namespace KitchenFlow.Api.Models
{
    public class AdjustInventoryRequest
    {
        public decimal Amount { get; set; } // 양수면 입고(+), 음수면 출고(-)
    }
}
