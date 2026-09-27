namespace KitchenFlow.Api.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // 1:N — 레시피 하나가 단계 여러 개
        public List<Step> Steps { get; set; } = new();
    }
}
