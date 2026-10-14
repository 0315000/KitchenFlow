namespace KitchenFlow.Api.Services
{
    // ① 입력 record — Step.cs에서 필요한 값만 (단계 구분 / 순서 / 소요시간 / 선행 단계)
    public record SimulationStep(int StepId, int Order, int DurationMinutes, IReadOnlyList<int> DependsOn);

    // ② 출력 record — stepId, startMin, endMin
    public record TimelineEntry(int StepId, int StartMin, int EndMin);

    // ③ 단계 목록 → 타임라인 (T7: 의존관계 기반)
    //  - 선행 단계가 모두 끝나야 시작한다. 선행이 없으면 0분에 시작 → 동시에 시작하는 단계가 생긴다.
    //  - 위상정렬(Kahn): 선행 단계가 항상 먼저 계산되도록 순서를 정한다. 재귀 없이 반복문만 쓴다.
    //  - 순환(서로를 기다림)이 있으면 null 을 돌려준다.
    //  - DB도 시간도 모른다: 같은 입력이면 항상 같은 결과 (DP4 결정 B)
    public static class RecipeSimulator
    {
        public static List<TimelineEntry>? Simulate(IReadOnlyList<SimulationStep> steps)
        {
            var byId = steps.ToDictionary(s => s.StepId);

            // ── 1. 준비 ──
            // 단계마다 "기다리는 단계 목록" (이상한 값을 걸러낸 것)
            var depsOf = new Dictionary<int, List<int>>();
            // 단계마다 "아직 안 끝난 선행 단계 수" (쪽지)
            var remaining = new Dictionary<int, int>();
            // 단계마다 "나를 기다리는 단계들" (화살표를 거꾸로 본 것)
            var waitingForMe = steps.ToDictionary(s => s.StepId, _ => new List<int>());

            foreach (var step in steps)
            {
                // 같은 레시피에 있는 단계만, 자기 자신은 빼고, 중복 없이
                var deps = step.DependsOn.Where(d => byId.ContainsKey(d) && d != step.StepId).Distinct().ToList();

                depsOf[step.StepId] = deps;
                remaining[step.StepId] = deps.Count;
                foreach (var d in deps)
                {
                    waitingForMe[d].Add(step.StepId);   // d 가 끝나면 step 의 기다림이 하나 줄어든다
                }
            }

            // ── 2. 지금 시작할 수 있는 단계 (쪽지가 0인 단계) ──
            var ready = steps.Where(s => remaining[s.StepId] == 0).Select(s => s.StepId).ToList();

            var endOf = new Dictionary<int, int>();       // 단계 Id → 끝 시각
            var timeline = new List<TimelineEntry>();

            // ── 3. 바구니가 빌 때까지 반복 ──
            while (ready.Count > 0)
            {
                // 여러 개면 Order 가 빠른 것부터 → 같은 입력이면 항상 같은 순서
                var id = ready.OrderBy(x => byId[x].Order).First();
                ready.Remove(id);
                var step = byId[id];

                // 시작 = 내가 기다리는 단계들 중 가장 늦게 끝난 시각 (없으면 0)
                int start = depsOf[id].Count == 0 ? 0 : depsOf[id].Max(d => endOf[d]);
                int end = start + step.DurationMinutes;

                endOf[id] = end;
                timeline.Add(new TimelineEntry(id, start, end));

                // 나를 기다리던 단계들의 쪽지를 1씩 줄이고, 0이 되면 바구니에 넣는다
                foreach (var next in waitingForMe[id])
                {
                    remaining[next]--;
                    if (remaining[next] == 0)
                    {
                        ready.Add(next);
                    }
                }
            }

            // ── 4. 처리 못 한 단계가 남았다면 순환 ──
            if (timeline.Count < steps.Count)
            {
                return null;
            }

            return timeline;
        }
    }
}
