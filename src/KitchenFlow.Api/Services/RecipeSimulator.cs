namespace KitchenFlow.Api.Services
{
    // ① 입력 record — Step.cs에서 필요한 값만 (단계 구분 / 순서 / 소요시간 / 선행 단계)
    public record SimulationStep(int MachineId, int StepId, int Order, int DurationMinutes, IReadOnlyList<int> DependsOn);

    // ② 출력 record — stepId, startMin, endMin
    public record TimelineEntry(int MachineId, int StepId, int StartMin, int EndMin);

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

            // ── 3. 위상 순서 만들기 (선행 단계가 항상 앞에 오는 순서) ──
            var order = new List<int>();
            while (ready.Count > 0)
            {
                // 여러 개면 Order 가 빠른 것부터 → 같은 입력이면 항상 같은 순서
                var id = ready.OrderBy(x => byId[x].Order).First();
                ready.Remove(id);

                order.Add(id);   // 시간 계산은 STEP 4 에서. 여기서는 순서만 기록한다

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
            if (order.Count < steps.Count)
            {
                return null;
            }

            // ── 5. 임계경로 값 = 내 시간 + 나를 기다리는 단계들 중 가장 긴 줄 ──
            //  - 위상 순서를 거꾸로 돌면 "나를 기다리는 단계"는 항상 먼저 계산되어 있다
            var critical = new Dictionary<int, int>();
            for (int i = order.Count - 1; i >= 0; i--)
            {
                int id = order[i];
                var after = waitingForMe[id];   // 나를 기다리는 단계들

                // 뒤에 이어진 줄 중 가장 긴 값 (뒤가 없으면 0)
                int longestAfter = after.Count == 0 ? 0 : after.Max(d => critical[d]);

                critical[id] = byId[id].DurationMinutes + longestAfter;
            }

            // ── 6. 시작 시각 배정 (기계 경합 + 우선순위 규칙) ──
            //  - 매번 "가장 일찍 시작할 수 있는 단계"들 중에서 규칙으로 하나 고른다 (그리디)
            //  - 규칙: 임계경로 큰 것 → 같으면 Order 작은 것
            var machineFreeAt = new Dictionary<int, int>();   // 기계 Id → 그 기계가 비는 시각

            while (timeline.Count < steps.Count)
            {
                // ① 후보: 아직 배정 안 됐고, 선행 단계가 전부 배정된 단계들
                var candidates = steps
                    .Where(s => !endOf.ContainsKey(s.StepId))                     // 아직 배정 안 됨
                    .Where(s => depsOf[s.StepId].All(d => endOf.ContainsKey(d)))  // 선행이 전부 배정됨
                    .ToList();

                // ② 후보마다 가장 빨리 시작할 수 있는 시각
                //    = max(선행 단계들 중 가장 늦게 끝난 시각, 내 기계가 비는 시각)
                int EarliestStart(SimulationStep s)
                {
                    int depsDone = depsOf[s.StepId].Count == 0 ? 0 : depsOf[s.StepId].Max(d => endOf[d]);
                    int machineFree = machineFreeAt.GetValueOrDefault(s.MachineId, 0);
                    return Math.Max(depsDone, machineFree);
                }

                // ③ 가장 이른 시각
                int now = candidates.Min(EarliestStart);

                // ④ 그 시각에 시작할 수 있는 후보들 중 규칙으로 하나 고르기
                var pick = candidates
                    .Where(s => EarliestStart(s) == now)
                    .OrderByDescending(s => critical[s.StepId])   // 규칙 1: 임계경로 큰 것
                    .ThenBy(s => s.Order)                         // 규칙 2: 같으면 Order 작은 것
                    .First();

                // ⑤ 배정
                int end = now + pick.DurationMinutes;
                endOf[pick.StepId] = end;
                machineFreeAt[pick.MachineId] = end;
                timeline.Add(new TimelineEntry(pick.MachineId, pick.StepId, now, end));
            }

            return timeline;
        }
    }
}
