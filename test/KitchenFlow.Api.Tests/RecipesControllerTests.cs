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
    }
}
