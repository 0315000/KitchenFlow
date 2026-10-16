namespace KitchenFlow.Api.Models
{
    // 레시피를 한 번 실행한 기록 (T9)
    public class Run
    {
        public int Id { get; set; }

        // 어느 레시피를 실행했는지 — FK는 N 쪽(Run)에 둔다
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public DateTime StartedAt { get; set; }                   // 실행한 시각
        public bool Succeeded { get; set; }                       // 성공 true / 실패 false
        public string? FailureReason { get; set; }                // 실패 사유 (성공이면 null)
        public string RecipeSnapshot { get; set; } = string.Empty; // 실행 시점의 레시피 (JSON)

        // 1:N — 실행 하나가 이벤트 여러 개
        public List<RunEvent> Events { get; set; } = new();
    }
}