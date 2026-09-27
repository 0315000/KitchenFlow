namespace KitchenFlow.Api.Models
{
    public class Step
    {
        public int Id { get; set; }

        // FK는 N 쪽(Step)에 둔다
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public int Order { get; set; }                         // 1, 2, 3 ...
        public int MachineId { get; set; }
        public Machine Machine { get; set; } = null!;
        public string Action { get; set; } = string.Empty;     // 끓이기, 굽기 ...
        public int DurationMinutes { get; set; }
        public int? TempC { get; set; }                        // 온도 없는 단계는 null
        public List<StepInput> Inputs { get; set; } = new();   // 이 단계에서 쓰는 재료들
    }
}
