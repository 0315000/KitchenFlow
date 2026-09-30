namespace KitchenFlow.Api.Services
{
    // 스케줄 계산에 필요한 단계 정보
    public record ScheduleInput(int StepId, int Order, int MachineId, int DurationMinutes, IReadOnlyCollection<int> DependsOn);

    // 계산 결과: 각 단계가 몇 분에 시작해서 몇 분에 끝나는지
    public record ScheduledStep(int StepId, int StartMinute, int EndMinute);

    // 레시피 한 개의 조리 단계를 "동시에 할 수 있는 건 동시에" 배치한다.
    //  - 규칙 1: 선행 단계가 모두 끝나야 시작할 수 있다.
    //  - 규칙 2: 같은 기계는 한 번에 한 단계만 쓸 수 있다.
    //  - 규칙 3: 동시에 시작할 수 있는 단계가 여러 개면 Order(화면 순서)가 빠른 것부터 배치한다.
    // 위상 정렬(Kahn 알고리즘)로 선행 단계가 항상 먼저 계산되게 한다.
    public static class RecipeScheduler
    {
        // 선후 관계에 순환(A→B→A)이 있으면 null 을 반환한다.
        public static List<ScheduledStep>? Compute(IReadOnlyList<ScheduleInput> steps)
        {
            var byId = steps.ToDictionary(s => s.StepId);

            // 같은 레시피 안의 단계만 선행으로 인정
            var deps = steps.ToDictionary(
                s => s.StepId,
                s => s.DependsOn.Where(d => byId.ContainsKey(d) && d != s.StepId).Distinct().ToList());

            var remaining = deps.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
            var dependents = steps.ToDictionary(s => s.StepId, _ => new List<int>());
            foreach (var (stepId, list) in deps)
            {
                foreach (var d in list)
                {
                    dependents[d].Add(stepId);
                }
            }

            var ready = steps.Where(s => remaining[s.StepId] == 0).Select(s => s.StepId).ToList();
            var endOf = new Dictionary<int, int>();
            var machineFreeAt = new Dictionary<int, int>();
            var result = new List<ScheduledStep>();

            while (ready.Count > 0)
            {
                // 지금 시작할 수 있는 단계 중 Order 가 가장 빠른 것
                var nextId = ready.OrderBy(id => byId[id].Order).First();
                ready.Remove(nextId);
                var step = byId[nextId];

                int afterDeps = deps[nextId].Count == 0 ? 0 : deps[nextId].Max(d => endOf[d]);
                int machineFree = machineFreeAt.TryGetValue(step.MachineId, out var t) ? t : 0;
                int start = Math.Max(afterDeps, machineFree);
                int end = start + step.DurationMinutes;

                endOf[nextId] = end;
                machineFreeAt[step.MachineId] = end;
                result.Add(new ScheduledStep(nextId, start, end));

                foreach (var dependent in dependents[nextId])
                {
                    remaining[dependent]--;
                    if (remaining[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }

            // 배치되지 못한 단계가 있다 = 서로를 기다리는 순환이 있다
            return result.Count == steps.Count ? result : null;
        }
    }
}
