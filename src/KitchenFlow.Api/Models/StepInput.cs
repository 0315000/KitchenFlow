namespace KitchenFlow.Api.Models
{
    public class StepInput
    {
        // 어느 단계에서
        public int StepId { get; set; }
        public Step Step { get; set; } = null!;

        // 어떤 재료를
        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;

        // 얼마나 쓰는지
        public decimal Quantity { get; set; }
    }
}
