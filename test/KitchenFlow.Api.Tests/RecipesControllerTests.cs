using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Controllers;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;
using Xunit;

namespace KitchenFlow.Api.Tests
{
    // T4 레시피/조리 단계 테스트
    // FK 제약(Cascade/Restrict)이 실제로 동작해야 하므로 InMemory가 아닌 SQLite 메모리 DB를 쓴다.
    // EnsureCreated()가 시드 데이터(라면 3단계 / 야채볶음 6단계 / 로스트치킨 12단계)를 함께 넣는다.
    public class RecipesControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly KitchenFlowDbContext _context;
        private readonly RecipesController _controller;

        public RecipesControllerTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            // 이미 열어둔 커넥션을 넘길 때는 외래키 제약을 직접 켜야 한다.
            using (var pragmaCommand = _connection.CreateCommand())
            {
                pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
                pragmaCommand.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new KitchenFlowDbContext(options);
            _context.Database.EnsureCreated();
            _controller = new RecipesController(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        private async Task<List<int>> StepIdsInOrder(int recipeId)
        {
            _context.ChangeTracker.Clear();
            return await _context.Steps
                .Where(s => s.RecipeId == recipeId)
                .OrderBy(s => s.Order)
                .Select(s => s.Id)
                .ToListAsync();
        }

        // ── 완료 조건 2: 레시피 삭제 시 동작이 티켓의 결정과 일치 ──

        [Fact]
        public async Task DeleteRecipe_CascadesStepsAndInputs_KeepsIngredientsAndMachines()
        {
            int ingredientCountBefore = await _context.Ingredients.CountAsync();
            int machineCountBefore = await _context.Machines.CountAsync();
            var ramenStepIds = await StepIdsInOrder(1);
            Assert.True(await _context.StepInputs.AnyAsync(i => ramenStepIds.Contains(i.StepId)));

            var result = await _controller.DeleteRecipe(1);   // 라면

            Assert.IsType<NoContentResult>(result);
            _context.ChangeTracker.Clear();
            Assert.False(await _context.Recipes.AnyAsync(r => r.Id == 1));
            Assert.False(await _context.Steps.AnyAsync(s => s.RecipeId == 1));                     // Cascade
            Assert.False(await _context.StepInputs.AnyAsync(i => ramenStepIds.Contains(i.StepId))); // Cascade
            Assert.Equal(ingredientCountBefore, await _context.Ingredients.CountAsync());           // 재료는 남음
            Assert.Equal(machineCountBefore, await _context.Machines.CountAsync());                 // 기계는 남음
            Assert.Equal(12, await _context.Steps.CountAsync(s => s.RecipeId == 3));                // 다른 레시피는 그대로
        }

        [Fact]
        public async Task DeleteRecipe_NotFound_Returns404()
        {
            var result = await _controller.DeleteRecipe(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteIngredient_UsedInStep_IsRejected()
        {
            var water = await _context.Ingredients.FindAsync(1);   // 라면 1단계에서 사용
            _context.Ingredients.Remove(water!);

            await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());   // Restrict
        }

        // ── 완료 조건 1: 12단계 레시피를 만들고 순서가 유지된다 ──

        [Fact]
        public async Task CreateRecipe_Add12Steps_OrderIsPersisted()
        {
            var created = await _controller.CreateRecipe(new RecipeCreateRequest { Name = "테스트 12단계" });
            Assert.IsType<CreatedAtActionResult>(created);
            int recipeId = await _context.Recipes.Where(r => r.Name == "테스트 12단계").Select(r => r.Id).SingleAsync();

            for (int i = 1; i <= 12; i++)
            {
                var result = await _controller.AddStep(recipeId, new StepCreateRequest
                {
                    MachineId = 1,
                    Action = $"단계 {i}",
                    DurationMinutes = i
                });
                Assert.IsType<OkObjectResult>(result);
            }

            // 새 컨텍스트로 다시 읽어도(= 새로고침) 1~12 순서 그대로
            _context.ChangeTracker.Clear();
            var actions = await _context.Steps
                .Where(s => s.RecipeId == recipeId)
                .OrderBy(s => s.Order)
                .Select(s => new { s.Order, s.Action })
                .ToListAsync();

            Assert.Equal(12, actions.Count);
            for (int i = 0; i < 12; i++)
            {
                Assert.Equal(i + 1, actions[i].Order);
                Assert.Equal($"단계 {i + 1}", actions[i].Action);
            }
        }

        [Fact]
        public async Task CreateRecipe_EmptyName_ReturnsBadRequest()
        {
            var result = await _controller.CreateRecipe(new RecipeCreateRequest { Name = "  " });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AddStep_NonExistentMachine_ReturnsBadRequest()
        {
            var result = await _controller.AddStep(1, new StepCreateRequest { MachineId = 999, Action = "x", DurationMinutes = 1 });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DeleteStep_RenumbersRemainingSteps()
        {
            // 라면: 1(id1) 2(id2) 3(id3) → 2번 삭제 → 1(id1) 2(id3)
            var result = await _controller.DeleteStep(1, 2);

            Assert.IsType<NoContentResult>(result);
            _context.ChangeTracker.Clear();
            var orders = await _context.Steps
                .Where(s => s.RecipeId == 1)
                .OrderBy(s => s.Order)
                .Select(s => new { s.Id, s.Order })
                .ToListAsync();
            Assert.Equal(2, orders.Count);
            Assert.Equal((1, 1), (orders[0].Id, orders[0].Order));
            Assert.Equal((3, 2), (orders[1].Id, orders[1].Order));
        }

        [Fact]
        public async Task MoveStep_UpThenDown_RestoresOrder()
        {
            await _controller.MoveStep(1, 3, "up");
            Assert.Equal(new List<int> { 1, 3, 2 }, await StepIdsInOrder(1));

            await _controller.MoveStep(1, 3, "down");
            Assert.Equal(new List<int> { 1, 2, 3 }, await StepIdsInOrder(1));
        }

        [Fact]
        public async Task MoveStep_TopUp_ReturnsBadRequest()
        { 
            var result = await _controller.MoveStep(1, 1, "up");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // ── 병렬 조리: 여러 단계를 동시에 진행해서 한 요리로 완성 ──

        private async Task<(int total, int sequential, Dictionary<int, (int start, int end)> times)> Schedule(int recipeId)
        {
            _context.ChangeTracker.Clear();
            var result = await _controller.GetSchedule(recipeId);
            var ok = Assert.IsType<OkObjectResult>(result);
            var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var times = new Dictionary<int, (int, int)>();
            foreach (var step in root.GetProperty("Steps").EnumerateArray())
            {
                times[step.GetProperty("Id").GetInt32()] =
                    (step.GetProperty("StartMinute").GetInt32(), step.GetProperty("EndMinute").GetInt32());
            }
            return (root.GetProperty("TotalMinutes").GetInt32(), root.GetProperty("SequentialMinutes").GetInt32(), times);
        }

        [Fact]
        public async Task Schedule_VegetableStirFry_RunsFirstThreeStepsInParallel()
        {
            var (total, sequential, t) = await Schedule(2);   // 야채볶음

            // 채소 계량(4) · 양념 섞기(5) · 팬 예열(6) 이 모두 0분에 동시에 시작
            Assert.Equal(0, t[4].start);
            Assert.Equal(0, t[5].start);
            Assert.Equal(0, t[6].start);
            // 볶기(7)는 계량(~2분)과 예열(~3분)이 둘 다 끝난 3분에 시작
            Assert.Equal(3, t[7].start);
            Assert.Equal(11, total);        // 동시에 하면 11분
            Assert.Equal(15, sequential);   // 하나씩 하면 15분
        }

        [Fact]
        public async Task Schedule_RoastChicken_PrepHappensWhileOvenPreheats()
        {
            var (total, sequential, t) = await Schedule(3);   // 로스트치킨

            Assert.Equal(0, t[10].start);           // 오븐 예열 0~10분
            Assert.True(t[14].end <= 10);           // 허브버터는 예열하는 동안 끝남
            Assert.Equal(10, t[15].start);          // 1차 굽기는 예열이 끝난 10분에 시작
            Assert.True(t[18].end <= t[17].end);    // 버터 소스는 굽는 동안 따로 준비
            Assert.True(total < sequential);
        }

        [Fact]
        public async Task SetDependencies_Cycle_ReturnsBadRequest()
        {
            // 야채볶음: 4 → 7 → 8 → 9 인데, 4가 9를 기다리게 하면 순환
            var result = await _controller.SetDependencies(2, 4, new StepDependenciesRequest { DependsOnStepIds = new List<int> { 9 } });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetDependencies_MakeStepParallel_ChangesSchedule()
        {
            // 야채볶음 "남은 재료 보관"(9, 냉장고)의 선행을 없애면 다른 단계와 동시에 0분에 시작
            var result = await _controller.SetDependencies(2, 9, new StepDependenciesRequest());

            Assert.IsType<NoContentResult>(result);
            var (_, _, t) = await Schedule(2);
            Assert.Equal(0, t[9].start);
        }

        [Fact]
        public async Task Schedule_SameMachine_NeverOverlaps()
        {
            // 라면 3단계(끓이기)의 선행을 없애도, 1단계와 같은 인덕션이라 동시에 못 하고 기다린다
            await _controller.SetDependencies(1, 3, new StepDependenciesRequest());

            var (_, _, t) = await Schedule(1);
            Assert.True(t[3].start >= t[1].end);
        }

        [Fact]
        public async Task AddStep_DefaultsToAfterLastStep()
        {
            await _controller.AddStep(1, new StepCreateRequest { MachineId = 4, Action = "그릇에 담기", DurationMinutes = 1 });

            var (total, _, _) = await Schedule(1);
            Assert.Equal(11, total);   // 라면 5+1+4 뒤에 1분 → 순차 11분
        }

        [Fact]
        public async Task DeleteStep_ReconnectsFlow()
        {
            // 라면 1 → 2 → 3 에서 2를 지우면 3은 1을 기다린다
            await _controller.DeleteStep(1, 2);

            var (_, _, t) = await Schedule(1);
            Assert.Equal(t[1].end, t[3].start);
        }
               // ── 여러 요리 주문: 기계 경합 + 한곳에 모아 서빙 ──

        private async Task<System.Text.Json.JsonElement> Combined(params int[] recipeIds)
        {
            _context.ChangeTracker.Clear();
            var result = await _controller.GetCombinedSchedule(recipeIds.ToList());
            var ok = Assert.IsType<OkObjectResult>(result);
            var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
            return System.Text.Json.JsonDocument.Parse(json).RootElement;
        }

        [Fact]
        public async Task CombinedSchedule_ServeAtSlowestDish()
        {
            var root = await Combined(2, 3);   // 야채볶음 + 로스트치킨

            Assert.Equal(87, root.GetProperty("ServeMinute").GetInt32());
            Assert.Equal(114, root.GetProperty("SequentialMinutes").GetInt32());

            var dishes = root.GetProperty("Dishes").EnumerateArray()
                .ToDictionary(d => d.GetProperty("RecipeId").GetInt32());
            Assert.Equal(11, dishes[2].GetProperty("FinishMinute").GetInt32());
            Assert.Equal(76, dishes[2].GetProperty("WaitBeforeServe").GetInt32());
            Assert.Equal(0, dishes[3].GetProperty("WaitBeforeServe").GetInt32());
        }

        [Fact]
        public async Task CombinedSchedule_SameMachine_NeverOverlaps()
        {
            var root = await Combined(1, 2, 3);   // 세 요리 모두

            var byMachine = root.GetProperty("Steps").EnumerateArray()
                .GroupBy(s => s.GetProperty("MachineId").GetInt32());
            foreach (var machine in byMachine)
            {
                var ordered = machine.OrderBy(s => s.GetProperty("StartMinute").GetInt32()).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    Assert.True(ordered[i].GetProperty("StartMinute").GetInt32()
                                >= ordered[i - 1].GetProperty("EndMinute").GetInt32());
                }
            }
        }

        [Fact]
        public async Task CombinedSchedule_ButterSauce_WaitsForInduction()
        {
            // 로스트치킨만: 버터 소스(id 18)는 인덕션이 비어 있으니 0분 시작
            var alone = await Combined(3);
            var aloneStart = alone.GetProperty("Steps").EnumerateArray()
                .Single(s => s.GetProperty("Id").GetInt32() == 18).GetProperty("StartMinute").GetInt32();
            Assert.Equal(0, aloneStart);

            // 야채볶음과 같이: 인덕션을 야채볶음이 10분까지 쓰므로 기다린다
            var together = await Combined(2, 3);
            var togetherStart = together.GetProperty("Steps").EnumerateArray()
                .Single(s => s.GetProperty("Id").GetInt32() == 18).GetProperty("StartMinute").GetInt32();
            Assert.Equal(10, togetherStart);
        }

        [Fact]
        public async Task CombinedSchedule_EmptyOrUnknown_IsRejected()
        {
            Assert.IsType<BadRequestObjectResult>(await _controller.GetCombinedSchedule(new List<int>()));
            Assert.IsType<NotFoundObjectResult>(await _controller.GetCombinedSchedule(new List<int> { 2, 999 }));
        } 
    }
}
