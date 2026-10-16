namespace KitchenFlow.Api.Models
{
    // 실행 안에서 일어난 일 하나 — 어떤 단계가 어떤 기계에서 몇 분부터 몇 분까지 (T9)
    public class RunEvent
    {
        public int Id { get; set; }

        // 어느 실행의 이벤트인지 — FK는 N 쪽(RunEvent)에 둔다
        public int RunId { get; set; }
        public Run Run { get; set; } = null!;

        // 레시피가 나중에 편집/삭제돼도 기록이 남도록 FK 없이 값만 복사해 둔다
        public int StepId { get; set; }
        public int MachineId { get; set; }

        public int StartMin { get; set; }   // 실행 시작 후 몇 분에 시작했는지
        public int EndMin { get; set; }     // 실행 시작 후 몇 분에 끝났는지
    }
}
