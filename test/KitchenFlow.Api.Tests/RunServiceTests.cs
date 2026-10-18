using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Services;
using Xunit;

namespace KitchenFlow.Api.Tests
{
    // T9 실행 기록 + 재고 차감 테스트 (DP3-2 엣지케이스 확인용)
    // 트랜잭션과 조건부 UPDATE(ExecuteUpdateAsync)가 실제로 동작해야 하므로 InMemory가 아닌 SQLite 메모리 DB를 쓴다.
    // 시드: 라면(Id 1) = 물 550ml(재고 10000) + 라면 1개(재고 20), 인덕션(Id 4)에서 100°C
    public class RunServiceTests : IDisposable
    {
        private const int Ramen = 1;
        private const int Water = 1;       // 재료 Id
        private const int RamenNoodle = 2; // 재료 Id
        private const int Induction = 4;   // 기계 Id

        private readonly SqliteConnection _connection;
        private readonly KitchenFlowDbContext _context;
        private readonly RunService _service;

        public RunServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

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
            _service = new RunService(_context);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        private async Task SetStock(int ingredientId, decimal qty)
        {
            await _context.Ingredients
                .Where(i => i.Id == ingredientId)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.StockQty, qty));
        }

        private async Task<decimal> Stock(int ingredientId)
        {
            return await _context.Ingredients
                .Where(i => i.Id == ingredientId)
                .Select(i => i.StockQty)
                .SingleAsync();
        }

        [Fact]
        public async Task Run_Success_DeductsExactlyAndSavesEvents()
        {
            var run = await _service.RunAsync(Ramen);

            Assert.NotNull(run);
            Assert.True(run!.Succeeded);
            Assert.Null(run.FailureReason);
            Assert.Equal(3, run.Events.Count);                 // 라면 3단계 = 막대 3개
            Assert.Equal(9450m, await Stock(Water));           // 10000 - 550
            Assert.Equal(19m, await Stock(RamenNoodle));       // 20 - 1
        }

        [Fact]
        public async Task Run_StockExactlyEnough_SucceedsAndLeavesZero()
        {
            await SetStock(RamenNoodle, 1);                    // 필요 1, 재고 1

            var run = await _service.RunAsync(Ramen);

            Assert.True(run!.Succeeded);
            Assert.Equal(0m, await Stock(RamenNoodle));
        }

        [Fact]
        public async Task Run_OneShort_FailsAndKeepsAllStock()
        {
            await SetStock(Water, 549);                        // 필요 550, 재고 549 (1 부족)

            var run = await _service.RunAsync(Ramen);

            Assert.False(run!.Succeeded);
            Assert.Contains("재고 부족", run.FailureReason);
            Assert.Empty(run.Events);                          // 실행하지 않았으니 타임라인 없음
            Assert.Equal(549m, await Stock(Water));            // 부족한 재료도
            Assert.Equal(20m, await Stock(RamenNoodle));       // 충분한 재료도 그대로 (절반 차감 없음)
            Assert.True(await _context.Runs.AnyAsync(r => r.Id == run.Id && !r.Succeeded));   // 실패도 기록된다
        }

        [Fact]
        public async Task Run_InvalidRecipe_FailsWithoutTouchingStock()
        {
            // 인덕션 최고 온도를 50°C로 낮추면 라면(100°C)은 검증 실패
            await _context.Machines
                .Where(m => m.Id == Induction)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.MaxTempC, 50));

            var run = await _service.RunAsync(Ramen);

            Assert.False(run!.Succeeded);
            Assert.StartsWith("검증 실패", run.FailureReason);
            Assert.Equal(10000m, await Stock(Water));
            Assert.Equal(20m, await Stock(RamenNoodle));
        }

        [Fact]
        public async Task Run_TwiceWithStockForOnce_OnlyFirstSucceeds()
        {
            await SetStock(RamenNoodle, 1);                    // 1회분만 남음

            var first = await _service.RunAsync(Ramen);
            var second = await _service.RunAsync(Ramen);

            Assert.True(first!.Succeeded);
            Assert.False(second!.Succeeded);
            Assert.Equal(0m, await Stock(RamenNoodle));        // 마이너스가 되지 않는다
        }

        [Fact]
        public async Task Run_SnapshotKeepsRecipeAsItWasWhenRun()
        {
            var run = await _service.RunAsync(Ramen);

            // 실행 뒤에 레시피 단계를 지워도
            var lastStep = await _context.Steps.Where(s => s.RecipeId == Ramen).OrderBy(s => s.Order).LastAsync();
            _context.Steps.Remove(lastStep);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // 그 실행의 스냅샷과 막대는 실행했던 때 모습(3단계) 그대로다
            var saved = await _service.GetRunAsync(Ramen, run!.Id);
            Assert.Equal(3, saved!.Events.Count);
            using var snapshot = JsonDocument.Parse(saved.RecipeSnapshot);
            Assert.Equal(3, snapshot.RootElement.GetProperty("Steps").GetArrayLength());
        }

        [Fact]
        public async Task Run_RecipeNotFound_ReturnsNull()
        {
            Assert.Null(await _service.RunAsync(999));
        }

        [Fact]
        public async Task GetRun_OtherRecipesRun_ReturnsNull()
        {
            var run = await _service.RunAsync(Ramen);

            Assert.Null(await _service.GetRunAsync(2, run!.Id));   // 야채볶음 주소로 라면의 실행을 달라고 하면 없음
        }
    }
}
