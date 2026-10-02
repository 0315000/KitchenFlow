using KitchenFlow.Api.Services;
using Xunit;

namespace KitchenFlow.Api.Tests
{
    // T5 실행 전 검사 — RecipeValidator 단위 테스트
    // DB(DbContext·SQLite)를 전혀 쓰지 않는다. record 를 직접 만들어 넣고 결과만 본다.
    // → 검증 로직이 DB와 분리된 순수 함수라는 증거
    public class RecipeValidatorTests
    {
        // ── 테스트용 기계 / 재료 (시드 값과 비슷하게) ──
        private static readonly Dictionary<int, ValidationMachine> Machines = new()
        {
            [4] = new ValidationMachine(4, "인덕션", CapacityMl: 5000, MinTempC: 30, MaxTempC: 240, IsAvailable: true),
        };

        private static readonly Dictionary<int, ValidationIngredient> Ingredients = new()
        {
            [1] = new ValidationIngredient(1, "물", "ml", StockQty: 10000),
            [3] = new ValidationIngredient(3, "양파", "g", StockQty: 100000),
            [9] = new ValidationIngredient(9, "버터", "g", StockQty: 50),
        };

        // 정상 단계 하나를 만든다. 테스트마다 "망가뜨릴 값"만 바꿔서 쓴다.
        // StepId 는 Order 와 같은 값으로 둔다.
        private static ValidationStep Step(
            int order,
            int machineId = 4,
            int duration = 5,
            int? temp = 100,
            IReadOnlyList<ValidationInput>? inputs = null)
            => new(order, order, machineId, duration, temp, inputs ?? []);

        private static List<Violation> Run(params ValidationStep[] steps)
            => RecipeValidator.Validate(steps, Machines, Ingredients);

        private static List<string> Codes(params ValidationStep[] steps)
            => Run(steps).Select(v => v.Code).ToList();

        // ── 정상 ──

        [Fact]
        public void ValidRecipe_HasNoViolations()
        {
            var codes = Codes(
                Step(1, inputs: [new ValidationInput(1, 550)]),   // 물 550ml
                Step(2),
                Step(3));

            Assert.Empty(codes);
        }

        // ── 빈 레시피 ──

        [Fact]
        public void EmptyRecipe_IsRejected()
        {
            var violations = Run();

            var v = Assert.Single(violations);
            Assert.Equal("EMPTY_RECIPE", v.Code);
            Assert.Null(v.StepId);   // 레시피 전체 위반이라 단계 정보 없음
        }

        // ── 소요시간 ≤ 0 ──

        [Fact]
        public void ZeroDuration_IsRejected()
        {
            Assert.Equal(["INVALID_DURATION"], Codes(Step(1, duration: 0)));
        }

        [Fact]
        public void NegativeDuration_IsRejected()
        {
            Assert.Equal(["INVALID_DURATION"], Codes(Step(1, duration: -3)));
        }

        // ── 없는 기계 ──

        [Fact]
        public void UnknownMachine_IsRejected_AndSkipsTempAndCapacityChecks()
        {
            // 온도 999°C 지만, 비교할 기계가 없으니 온도 위반은 나오지 않아야 한다
            var codes = Codes(Step(1, machineId: 999, temp: 999));

            Assert.Equal(["UNKNOWN_MACHINE"], codes);
        }

        // ── 온도 범위 ──

        [Fact]
        public void TempAboveMax_IsRejected()
        {
            Assert.Equal(["TEMP_OUT_OF_RANGE"], Codes(Step(1, temp: 300)));
        }

        [Fact]
        public void TempBelowMin_IsRejected()
        {
            Assert.Equal(["TEMP_OUT_OF_RANGE"], Codes(Step(1, temp: 10)));
        }

        [Fact]
        public void TempExactlyAtMax_IsAllowed()
        {
            Assert.Empty(Codes(Step(1, temp: 240)));   // 경계값은 허용
        }

        [Fact]
        public void NoTemp_IsNotChecked()
        {
            Assert.Empty(Codes(Step(1, temp: null)));  // 채소 계량처럼 온도 없는 단계
        }

        // ── 재고 부족 ──

        [Fact]
        public void NotEnoughStock_IsRejected()
        {
            var codes = Codes(Step(1, inputs: [new ValidationInput(9, 60)]));   // 버터 60g > 재고 50g

            Assert.Equal(["OUT_OF_STOCK"], codes);
        }

        [Fact]
        public void UsingExactlyAllStock_IsAllowed()
        {
            // T3 엣지케이스 6행 "딱 다 빼기"와 같은 경계
            Assert.Empty(Codes(Step(1, inputs: [new ValidationInput(9, 50)])));
        }

        [Fact]
        public void StockIsSummedAcrossSteps_ReportedOnceAtTheStepThatRunsOut()
        {
            // 버터 30g + 30g = 60g > 50g. 단계별로 보면 둘 다 통과라서 놓치기 쉬운 경우
            var violations = Run(
                Step(1, inputs: [new ValidationInput(9, 30)]),
                Step(2, inputs: [new ValidationInput(9, 30)]),
                Step(3, inputs: [new ValidationInput(9, 30)]));

            var v = Assert.Single(violations);          // 3단계에서 또 알리지 않는다
            Assert.Equal("OUT_OF_STOCK", v.Code);
            Assert.Equal(2, v.StepId);                  // 재고를 처음 넘긴 단계
        }

        // ── 기계 용량 ──

        [Fact]
        public void OverCapacity_IsRejected()
        {
            var codes = Codes(Step(1, inputs: [new ValidationInput(1, 6000)]));   // 물 6000ml > 인덕션 5000ml

            Assert.Equal(["OVER_CAPACITY"], codes);
        }

        [Fact]
        public void Capacity_IgnoresNonMlIngredients()
        {
            // 양파 9000g: 숫자는 5000보다 크지만 g 단위라 부피를 알 수 없어 검사 대상이 아니다
            Assert.Empty(Codes(Step(1, inputs: [new ValidationInput(3, 9000)])));
        }

        // ── 완료 조건: 망가뜨린 레시피 1개 → 위반 5개가 한 번에 ──

        [Fact]
        public void BrokenRecipe_ReportsAllFiveViolationsAtOnce()
        {
            var violations = Run(
                // 1단계: 소요시간 0분 + 물 6000ml(용량 초과) + 300°C(온도 초과)
                Step(1, duration: 0, temp: 300, inputs: [new ValidationInput(1, 6000)]),
                // 2단계: 없는 기계 + 버터 60g(재고 부족)
                Step(2, machineId: 999, inputs: [new ValidationInput(9, 60)]));

            Assert.Equal(5, violations.Count);
            Assert.Equal(
                ["INVALID_DURATION", "OUT_OF_STOCK", "OVER_CAPACITY", "TEMP_OUT_OF_RANGE", "UNKNOWN_MACHINE"],
                violations.Select(v => v.Code).Order().ToList());
        }
    }
}
