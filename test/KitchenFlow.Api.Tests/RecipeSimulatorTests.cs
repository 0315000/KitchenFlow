using KitchenFlow.Api.Services;
using Xunit;

namespace KitchenFlow.Api.Tests
{
    // T7 의존관계 기반 시뮬레이션 — RecipeSimulator 단위 테스트
    // DB 없이 단계와 선행 관계를 직접 만들어 넣고 결과만 본다. (T5 와 같은 방식)
    public class RecipeSimulatorTests
    {
        // 단계 하나를 만든다. StepId, Order, MachineId 는 같은 값으로 둔다. (단계마다 기계가 달라 경합 없음)
        // 예: Step(3, 5, 1, 2) → 3번 단계, 5분, 1번·2번이 끝나야 시작
        private static SimulationStep Step(int id, int duration, params int[] dependsOn)
            => new(id, id, id, duration, dependsOn);

        // 기계를 직접 정하는 단계. 같은 machine 을 주면 기계 경합이 생긴다.
        // 예: OnMachine(1, Oven, 40) → 1번 단계, 오븐, 40분, 선행 없음
        private static SimulationStep OnMachine(int id, int machine, int duration, params int[] dependsOn)
            => new(machine, id, id, duration, dependsOn);

        private const int Oven = 1;
        private const int Induction = 4;

        // 결과를 "단계 Id → (시작, 끝)" 사전으로 바꿔서 꺼내 보기 쉽게 한다.
        private static Dictionary<int, (int start, int end)> Run(params SimulationStep[] steps)
            => RecipeSimulator.Simulate(steps)!.ToDictionary(t => t.StepId, t => (t.StartMin, t.EndMin));

        // ── 위상정렬 5케이스 (티켓 완료 조건) ──

        [Fact]
        public void EmptyGraph_ReturnsEmptyTimeline()
        {
            var timeline = RecipeSimulator.Simulate([]);

            Assert.NotNull(timeline);   // 순환이 아니므로 null 이 아니다
            Assert.Empty(timeline);     // 단계가 없으니 결과도 없다
        }

        [Fact]
        public void SingleNode_StartsAtZero()
        {
            var t = Run(Step(1, 5));

            Assert.Equal((0, 5), t[1]);    // 기다릴 것 없음 → 0분 시작, 0 + 5 = 5분 끝
        }

        [Fact]
        public void Chain_RunsOneAfterAnother()
        {
            // 1 → 2 → 3 (라면과 같은 모양)
            var t = Run(
                Step(1, 5),
                Step(2, 1, 1),
                Step(3, 4, 2));

            Assert.Equal((0, 5), t[1]);    // 기다릴 것 없음 → 0분 시작, 0 + 5 = 5분 끝
            Assert.Equal((5, 6), t[2]);    // 1번(5분 끝)을 기다림 → 5분 시작, 5 + 1 = 6분 끝
            Assert.Equal((6, 10), t[3]);   // 2번(6분 끝)을 기다림 → 6분 시작, 6 + 4 = 10분 끝
        }

        [Fact]
        public void Diamond_JoinWaitsForSlowerBranch()
        {
            //      1 (2분)
            //     ↙      ↘
            //  2 (3분)  3 (5분)
            //     ↘      ↙
            //      4 (1분)     ← 2와 3이 둘 다 끝나야 시작
            var t = Run(
                Step(1, 2),
                Step(2, 3, 1),
                Step(3, 5, 1),
                Step(4, 1, 2, 3));

            Assert.Equal((2, 5), t[2]);    // 1번(2분 끝)을 기다림 → 2분 시작, 2 + 3 = 5분 끝
            Assert.Equal((2, 7), t[3]);    // 2와 같은 시각에 시작 = 병렬, 2 + 5 = 7분 끝
            Assert.Equal((7, 8), t[4]);    // 느린 쪽(3, 7분 끝)을 기다려야 한다 → 7분 시작, 8분 끝
        }

        [Fact]
        public void Cycle_ReturnsNull_WithoutInfiniteLoop()
        {
            // 1 → 2 → 1 (서로를 기다림)
            var timeline = RecipeSimulator.Simulate([
                Step(1, 5, 2),
                Step(2, 3, 1)]);

            Assert.Null(timeline);         // 순환이면 null (무한루프 없이 끝나야 이 줄에 도달한다)
        }

        // ── 실제 레시피: 야채볶음 (시드와 같은 값) ──

        [Fact]
        public void VegetableStirFry_FirstThreeStepsStartTogether()
        {
            // 4 계량 · 5 양념 · 6 예열 은 선행이 없다 → 0분에 동시에 시작
            var t = Run(
                Step(4, 2),
                Step(5, 2),
                Step(6, 3),
                Step(7, 5, 4, 6),
                Step(8, 2, 5, 7),
                Step(9, 1, 8));

            Assert.Equal(0, t[4].start);
            Assert.Equal(0, t[5].start);
            Assert.Equal(0, t[6].start);
            Assert.Equal(3, t[7].start);                // 4(~2분)와 6(~3분)이 둘 다 끝난 뒤
            Assert.Equal(11, t.Values.Max(x => x.end));  // 총 소요시간 (순차로 하면 15분)
        }

        // ── T8: 기계 경합 ──

        [Fact]
        public void SameMachine_NoDeps_HigherCriticalFirst()
        {
            // 감자(오븐 40), 당근(오븐 15) → 0분에 둘 다 오븐을 원한다
            var t = Run(
                OnMachine(1, Oven, 40),
                OnMachine(2, Oven, 15));

            Assert.Equal((0, 40), t[1]);    // 임계경로 40 > 15 → 감자 먼저
            Assert.Equal((40, 55), t[2]);   // 오븐이 빌 때까지 대기 → 40분 시작
        }

        [Fact]
        public void ShortStepWithLongTail_GoesFirst()
        {
            // 감자(오븐 40), 닭(오븐 15) → 닭 뒤에 소스(인덕션 30)가 이어진다
            // 임계경로: 감자 40, 닭 15 + 30 = 45 → 단계 하나는 짧아도 닭이 먼저
            var t = Run(
                OnMachine(1, Oven, 40),
                OnMachine(2, Oven, 15),
                OnMachine(3, Induction, 30, 2));

            Assert.Equal((0, 15), t[2]);    // 닭이 먼저 오븐에 들어간다 (규칙 1)
            Assert.Equal((15, 55), t[1]);   // 감자는 오븐이 빌 때까지 대기
            Assert.Equal((15, 45), t[3]);   // 소스는 닭이 끝나자마자 인덕션에서 시작
            Assert.Equal(55, t.Values.Max(x => x.end));   // 총 55분 (감자 먼저였다면 85분)
        }

        // ── T10 오븐 예열 (DP3-2 엣지케이스 9~14) ──

        // 오븐 단계: 온도를 주고 예열이 필요한 기계로 표시한다. temp 가 null 이면 온도 없는 단계(휴지)
        private static SimulationStep OvenStep(int id, int duration, int? temp, params int[] dependsOn)
            => new(Oven, id, id, duration, dependsOn, temp, NeedsPreheat: true);

        // 굽기 막대만 "단계 Id → (시작, 끝)" 으로, 예열 막대는 따로 "단계 Id → (시작, 끝)" 으로 나눠 본다
        private static (Dictionary<int, (int start, int end)> steps, Dictionary<int, (int start, int end)> preheats) RunWithPreheat(params SimulationStep[] steps)
        {
            var timeline = RecipeSimulator.Simulate(steps)!;
            return (
                timeline.Where(t => !t.IsPreheat).ToDictionary(t => t.StepId, t => (t.StartMin, t.EndMin)),
                timeline.Where(t => t.IsPreheat).ToDictionary(t => t.StepId, t => (t.StartMin, t.EndMin)));
        }

        [Fact]
        public void Preheat_FirstOvenUse_AddsPreheatBeforeBaking()
        {
            // 엣지 9: 처음 쓰는 오븐을 200°C로 → 200 ÷ 100 × 5 = 10분 예열
            var (t, p) = RunWithPreheat(OvenStep(1, 30, 200));

            Assert.Equal((0, 10), p[1]);    // 0분부터 10분까지 예열
            Assert.Equal((10, 40), t[1]);   // 예열이 끝나야 굽기 시작
        }

        [Fact]
        public void Preheat_SameTempAgain_IsSkipped()
        {
            // 엣지 10: 200°C 굽기 다음 같은 오븐 200°C → 이미 달궈져 있으니 예열 없음
            var (t, p) = RunWithPreheat(OvenStep(1, 30, 200), OvenStep(2, 25, 200, 1));

            Assert.Single(p);               // 예열은 처음 한 번뿐
            Assert.Equal((40, 65), t[2]);   // 1번(40분 끝) 바로 뒤에 시작
        }

        [Fact]
        public void Preheat_HigherTemp_OnlyTheMissingPart_RoundedUp()
        {
            // 엣지 11: 200°C 다음 230°C → 모자란 30°C → 1.5분 → 올림 2분
            var (t, p) = RunWithPreheat(OvenStep(1, 30, 200), OvenStep(2, 10, 230, 1));

            Assert.Equal((40, 42), p[2]);
            Assert.Equal((42, 52), t[2]);
        }

        [Fact]
        public void Preheat_LowerTemp_IsSkipped()
        {
            // 200°C 다음 150°C → 식히는 시간은 무시하고 예열도 없음
            var (t, p) = RunWithPreheat(OvenStep(1, 30, 200), OvenStep(2, 10, 150, 1));

            Assert.False(p.ContainsKey(2));
            Assert.Equal((40, 50), t[2]);
        }

        [Fact]
        public void Preheat_OvenStepWithoutTemp_IsSkipped()
        {
            // 엣지 12: 온도가 없는 오븐 단계(휴지) → 예열 없음
            var (t, p) = RunWithPreheat(OvenStep(1, 10, null));

            Assert.Empty(p);
            Assert.Equal((0, 10), t[1]);
        }

        [Fact]
        public void Preheat_DoesNotWaitForPreviousStep()
        {
            // R4: 예열은 재료가 필요 없어서 앞 단계(손질 15분)를 기다리지 않는다
            //     → 굽기 시작(15분)에 딱 맞춰 5~15분에 예열, 굽기가 예열 때문에 늦어지지 않는다
            var (t, p) = RunWithPreheat(
                OnMachine(1, Induction, 15),
                OvenStep(2, 30, 200, 1));

            Assert.Equal((5, 15), p[2]);
            Assert.Equal((15, 45), t[2]);
        }

        [Fact]
        public void Preheat_OccupiesOven_OtherOvenStepWaits()
        {
            // 엣지 13: 예열도 오븐을 쓰는 시간 → 다른 오븐 단계와 겹치지 않는다
            var (t, p) = RunWithPreheat(OvenStep(1, 30, 200), OvenStep(2, 20, 100));

            var bars = new[] { p[1], t[1], t[2] }.OrderBy(b => b.start).ToList();
            for (int i = 1; i < bars.Count; i++)
            {
                Assert.True(bars[i].start >= bars[i - 1].end);   // 오븐 막대끼리 겹치지 않는다
            }
        }

        [Fact]
        public void Preheat_NotRequested_KeepsOldBehavior()
        {
            // 하위 호환: 온도가 있어도 예열 표시(NeedsPreheat)가 없으면 예전과 똑같이 0분 시작
            var timeline = RecipeSimulator.Simulate([new SimulationStep(Oven, 1, 1, 30, [], 200)])!;

            Assert.Single(timeline);
            Assert.Equal(0, timeline[0].StartMin);
        }
    }
}
