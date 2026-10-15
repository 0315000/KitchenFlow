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
    }
}
