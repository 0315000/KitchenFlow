namespace KitchenFlow.Api.Models
{
    public class CookingTask
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // DP3-1: 필요한 기계 ID (문제1의 Machine 데이터 참조), 필수
        public int RequiredMachineId { get; set; }

        // DP3-1: 이 작업이 기계를 점유하는 소요 시간(분), 양의 정수만 허용
        public int DurationMinutes { get; set; }

        // DP3-1: 선행 작업(선택) — 이 작업이 시작되려면 먼저 끝나야 하는 작업의 Id
        public int? PrecedenceTaskId { get; set; }

        // DP3-1: (선택) 필요한 재료 — 문제2의 재고 데이터 참조
        public int? RequiredIngredientId { get; set; }
        public decimal? RequiredIngredientAmount { get; set; }
    }
}
