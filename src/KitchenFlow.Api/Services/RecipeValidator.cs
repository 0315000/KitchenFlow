namespace KitchenFlow.Api.Services
{
    // ── 입력: 검증에 필요한 값만 담은 "사진" (EF 엔티티를 그대로 넘기지 않는다) ──
    public record ValidationInput(int IngredientId, decimal Quantity);

    public record ValidationStep(
        int StepId,
        int Order,
        int MachineId,
        int DurationMinutes,
        int? TempC,
        IReadOnlyList<ValidationInput> Inputs);

    public record ValidationMachine(int Id, string Name, int CapacityMl, int MinTempC, int MaxTempC, bool IsAvailable);

    public record ValidationIngredient(int Id, string Name, string Unit, decimal StockQty);

    // ── 출력: 위반 하나 ──
    // 레시피 전체에 대한 위반(빈 레시피 등)은 StepId / Order 가 null
    public record Violation(string Code, string Message, int? StepId = null, int? Order = null);

    // 레시피를 실행하기 전에 "돌릴 수 있는 레시피인가"를 판단한다.
    //  - DB를 모른다: 필요한 값은 전부 매개변수로 받는다 (순수 함수)
    //  - 첫 위반에서 멈추지 않는다: 위반을 전부 모아서 한 번에 돌려준다
    public static class RecipeValidator
    {
        public static List<Violation> Validate(
            IReadOnlyList<ValidationStep> steps,
            IReadOnlyDictionary<int, ValidationMachine> machines,
            IReadOnlyDictionary<int, ValidationIngredient> ingredients)
        {
            var violations = new List<Violation>();

            // ① 빈 레시피
            if (steps.Count == 0)
            {
                violations.Add(new Violation("EMPTY_RECIPE", "단계가 하나도 없는 레시피입니다."));
            }

            // 재료별 누적 사용량 (여러 단계에서 같은 재료를 쓰면 합쳐서 재고와 비교)
            var usedSoFar = new Dictionary<int, decimal>();

            foreach (var step in steps.OrderBy(s => s.Order))
            {
                // ② 소요시간 ≤ 0
                if (step.DurationMinutes <= 0)
                {
                    violations.Add(new Violation(
                        "INVALID_DURATION",
                        $"{step.Order}단계: 소요 시간은 1분 이상이어야 합니다. (현재 {step.DurationMinutes}분)",
                        step.StepId, step.Order));
                }

                // ③ 재고 부족 — 기계 검사보다 먼저 (④에서 continue 하면 건너뛰므로)
                foreach (var input in step.Inputs)
                {
                    if (!ingredients.TryGetValue(input.IngredientId, out var ingredient))
                    {
                        continue;   // FK(Restrict) 때문에 실제로는 생기지 않는다
                    }

                    decimal before = usedSoFar.GetValueOrDefault(input.IngredientId);
                    decimal after = before + input.Quantity;
                    usedSoFar[input.IngredientId] = after;

                    // 재고를 "처음 넘기는" 단계에서 한 번만 알린다
                    if (before <= ingredient.StockQty && after > ingredient.StockQty)
                    {
                        violations.Add(new Violation(
                            "OUT_OF_STOCK",
                            $"{step.Order}단계: {ingredient.Name} 재고 부족 (누적 필요 {after}{ingredient.Unit}, 재고 {ingredient.StockQty}{ingredient.Unit})",
                            step.StepId, step.Order));
                    }
                }

                // ④ 없는 기계
                if (!machines.TryGetValue(step.MachineId, out var machine))
                {
                    violations.Add(new Violation(
                        "UNKNOWN_MACHINE",
                        $"{step.Order}단계: 존재하지 않는 기계입니다. (기계 Id {step.MachineId})",
                        step.StepId, step.Order));
                    continue;   // 기계가 없으면 용량·온도는 비교할 기준이 없다
                }

                // ⑤ 용량 초과
                // 단위가 ml인 재료만 합산 (g·개는 부피를 알 수 없어서 제외)
                decimal volumeMl = step.Inputs
                    .Where(i => ingredients.TryGetValue(i.IngredientId, out var g) && g.Unit == "ml")
                    .Sum(i => i.Quantity);
                if (volumeMl > machine.CapacityMl)
                {
                    violations.Add(new Violation(
                        "OVER_CAPACITY",
                        $"{step.Order}단계: 투입량 {volumeMl}ml가 {machine.Name} 용량({machine.CapacityMl}ml)을 넘습니다.",
                        step.StepId, step.Order));
                }

                // ⑥ 온도 범위 밖 (경계값은 허용, 온도 없는 단계는 검사 안 함)
                if (step.TempC is int temp && (temp < machine.MinTempC || temp > machine.MaxTempC))
                {
                    violations.Add(new Violation(
                        "TEMP_OUT_OF_RANGE",
                        $"{step.Order}단계: {temp}°C는 {machine.Name}의 허용 범위({machine.MinTempC}~{machine.MaxTempC}°C) 밖입니다.",
                        step.StepId, step.Order));
                }
            }

            return violations;   // return 은 맨 마지막 한 번만
        }
    }
}
