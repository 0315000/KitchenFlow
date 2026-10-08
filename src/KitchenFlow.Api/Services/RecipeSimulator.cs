namespace KitchenFlow.Api.Services
{
    // ① 입력 record — Step.cs에서 필요한 값만 (단계 구분 / 순서 / 소요시간)
    public record SimulationStep(int StepId, int Order, int DurationMinutes);

    // ② 출력 record — stepId, startMin, endMin
    public record TimelineEntry(int StepId, int StartMin, int EndMin);

    // ③ 단계 목록 → 순차 타임라인
    //  - 순차 실행만: Order 순으로 한 줄로 세우고, 앞 단계가 끝나야 다음 단계가 시작한다.
    //  - DB도 시간도 모른다: 같은 입력이면 항상 같은 결과 (DP4 결정 B)
    public static class RecipeSimulator
    {
        public static List<TimelineEntry> Simulate(IReadOnlyList<SimulationStep> steps)
        {
            var timeline = new List<TimelineEntry>();   // 결과를 적을 표
            int now = 0;                                // 지금 시각 (아직 아무것도 안 했으니 0분)

            foreach (var step in steps.OrderBy(s => s.Order))   // 화면 순서(Order)대로 한 단계씩
            {
                int start = now;                                 // 시작 = 지금 시각
                int end = start + step.DurationMinutes;          // 끝 = 시작 + 소요시간
                timeline.Add(new TimelineEntry(step.StepId, start, end));
                now = end;                                       // 다음 단계는 이 단계가 끝난 시각에 시작
            }

            return timeline;
        }
    }
}
