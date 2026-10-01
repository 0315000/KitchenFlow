namespace KitchenFlow.Api.Models
{
    // 단계 간 선후 관계 (DP2 개념 2: 순서를 숫자가 아니라 "연결"로 표현)
    // "StepId 단계는 DependsOnStepId 단계가 끝나야 시작할 수 있다"
    // 선행 단계가 없는 단계끼리는 동시에(병렬로) 진행할 수 있다.
    public class StepDependency
    {
        public int StepId { get; set; }
        public Step Step { get; set; } = null!;

        public int DependsOnStepId { get; set; }
        public Step DependsOn { get; set; } = null!;
    }
}
