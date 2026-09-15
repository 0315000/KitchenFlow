namespace KitchenFlow.Api.Models
{
    public class ScheduledCookingTask
    {
        public int TaskId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int RequiredMachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int? PrecedenceTaskId { get; set; }
        public bool MachineAvailable { get; set; }

        // null이면 아직 배정 불가(기계 불가/선행 작업 미배정) 상태
        public int? StartMinute { get; set; }
        public int? EndMinute { get; set; }

        // 같은 기계를 다른 작업이 먼저 쓰고 있어서 생긴 대기 시간(분)
        public int WaitMinutes { get; set; }

        // (선택) 필요 재료가 부족한 경우 true — 등록/배정을 막지는 않고 경고만 표시
        public bool IngredientShortage { get; set; }
    }
}
