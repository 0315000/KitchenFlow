namespace KitchenFlow.Api.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal Threshold { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
} 
