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

            // TODO(할 일 2): 규칙 6가지를 여기에 하나씩 추가

            return violations;   // return 은 맨 마지막 한 번만
        }
    }
}
