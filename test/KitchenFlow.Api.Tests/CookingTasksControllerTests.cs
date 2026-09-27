using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KitchenFlow.Api.Controllers;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;
using Xunit;

namespace KitchenFlow.Api.Tests
{
    public class CookingTasksControllerTests
    {
        private static KitchenFlowDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            var context = new KitchenFlowDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task CreateCookingTask_NonExistentMachine_ReturnsBadRequest()
        {
            using var context = CreateContext(nameof(CreateCookingTask_NonExistentMachine_ReturnsBadRequest));
            var controller = new CookingTasksController(context);

            var task = new CookingTask { Name = "반죽하기", RequiredMachineId = 999, DurationMinutes = 10 };

            var result = await controller.CreateCookingTask(task);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateCookingTask_DurationZero_ReturnsBadRequest()
        {
            using var context = CreateContext(nameof(CreateCookingTask_DurationZero_ReturnsBadRequest));
            var controller = new CookingTasksController(context);

            var task = new CookingTask { Name = "굽기", RequiredMachineId = 1, DurationMinutes = 0 };

            var result = await controller.CreateCookingTask(task);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateCookingTask_ValidInput_ReturnsCreated()
        {
            using var context = CreateContext(nameof(CreateCookingTask_ValidInput_ReturnsCreated));
            var controller = new CookingTasksController(context);

            var task = new CookingTask { Name = "굽기", RequiredMachineId = 1, DurationMinutes = 20 };

            var result = await controller.CreateCookingTask(task);

            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async Task UpdateCookingTask_CircularPrecedence_ReturnsBadRequest()
        {
            using var context = CreateContext(nameof(UpdateCookingTask_CircularPrecedence_ReturnsBadRequest));
            var controller = new CookingTasksController(context);

            var createA = await controller.CreateCookingTask(
                new CookingTask { Name = "A", RequiredMachineId = 1, DurationMinutes = 10 });
            var taskA = (CookingTask)((CreatedAtActionResult)createA).Value!;

            var createB = await controller.CreateCookingTask(
                new CookingTask { Name = "B", RequiredMachineId = 2, DurationMinutes = 10, PrecedenceTaskId = taskA.Id });
            var taskB = (CookingTask)((CreatedAtActionResult)createB).Value!;

            // A의 선행을 B로 바꾸면 A -> B -> A 순환이 생긴다 -> 거부되어야 함
            taskA.PrecedenceTaskId = taskB.Id;
            var updateResult = await controller.UpdateCookingTask(taskA.Id, taskA);

            Assert.IsType<BadRequestObjectResult>(updateResult);
        }

        [Fact]
        public async Task GetSchedule_SameMachine_SequencedWithoutOverlap()
        {
            using var context = CreateContext(nameof(GetSchedule_SameMachine_SequencedWithoutOverlap));
            var controller = new CookingTasksController(context);

            await controller.CreateCookingTask(new CookingTask { Name = "1번", RequiredMachineId = 1, DurationMinutes = 30 });
            await controller.CreateCookingTask(new CookingTask { Name = "2번", RequiredMachineId = 1, DurationMinutes = 20 });

            var result = await controller.GetSchedule();
            var schedule = (List<ScheduledCookingTask>)((OkObjectResult)result).Value!;

            var first = schedule[0];
            var second = schedule[1];

            Assert.Equal(0, first.StartMinute);
            Assert.Equal(30, first.EndMinute);
            Assert.Equal(30, second.StartMinute);
            Assert.Equal(50, second.EndMinute);
            Assert.Equal(30, second.WaitMinutes);
        }

        [Fact]
        public async Task GetSchedule_MachineUnavailable_TaskBlocked()
        {
            using var context = CreateContext(nameof(GetSchedule_MachineUnavailable_TaskBlocked));

            var machine = await context.Machines.FindAsync(1);
            machine!.IsAvailable = false;
            await context.SaveChangesAsync();

            var controller = new CookingTasksController(context);
            await controller.CreateCookingTask(new CookingTask { Name = "고장난기계작업", RequiredMachineId = 1, DurationMinutes = 10 });

            var result = await controller.GetSchedule();
            var schedule = (List<ScheduledCookingTask>)((OkObjectResult)result).Value!;

            Assert.Null(schedule[0].StartMinute);
            Assert.False(schedule[0].MachineAvailable);
        }

        [Fact]
        public async Task GetSchedule_Precedence_StartsAfterPrecedenceEnds()
        {
            using var context = CreateContext(nameof(GetSchedule_Precedence_StartsAfterPrecedenceEnds));
            var controller = new CookingTasksController(context);

            var createA = await controller.CreateCookingTask(
                new CookingTask { Name = "A", RequiredMachineId = 1, DurationMinutes = 15 });
            var taskA = (CookingTask)((CreatedAtActionResult)createA).Value!;

            await controller.CreateCookingTask(
                new CookingTask { Name = "B", RequiredMachineId = 2, DurationMinutes = 10, PrecedenceTaskId = taskA.Id });

            var result = await controller.GetSchedule();
            var schedule = (List<ScheduledCookingTask>)((OkObjectResult)result).Value!;

            var scheduledB = schedule.First(s => s.Name == "B");
            Assert.Equal(15, scheduledB.StartMinute);
        }

        [Fact]
        public async Task GetSchedule_IngredientShortage_FlagsWarning()
        {
            using var context = CreateContext(nameof(GetSchedule_IngredientShortage_FlagsWarning));

            var ingredient = new InventoryItem
            {
                ItemName = "밀가루",
                Quantity = 1,
                ExpiryDate = DateTime.Today.AddDays(10),
                Threshold = 0
            };
            context.InventoryItems.Add(ingredient);
            await context.SaveChangesAsync();

            var controller = new CookingTasksController(context);
            await controller.CreateCookingTask(new CookingTask
            {
                Name = "반죽하기",
                RequiredMachineId = 1,
                DurationMinutes = 10,
                RequiredIngredientId = ingredient.Id,
                RequiredIngredientAmount = 5
            });

            var result = await controller.GetSchedule();
            var schedule = (List<ScheduledCookingTask>)((OkObjectResult)result).Value!;

            Assert.True(schedule[0].IngredientShortage);
        }

        private class SqliteTestDb : IDisposable
        {
            public SqliteConnection Connection { get; }
            public KitchenFlowDbContext Context { get; }

            public SqliteTestDb(string connectionString)
            {
                Connection = new SqliteConnection(connectionString);
                Connection.Open();

                // SQLite는 외래키 제약이 기본적으로 꺼져 있다. EF Core가 커넥션을 직접 열 때는
                // 자동으로 켜주지만, 이렇게 이미 열어둔 커넥션을 넘길 때는 우리가 직접 켜야
                // Restrict(ON DELETE RESTRICT) 제약이 실제로 동작한다.
                using (var pragmaCommand = Connection.CreateCommand())
                {
                    pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
                    pragmaCommand.ExecuteNonQuery();
                }

                var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
                    .UseSqlite(Connection)
                    .Options;
                Context = new KitchenFlowDbContext(options);
                Context.Database.EnsureCreated();
            }

            public void Dispose()
            {
                Context.Dispose();
                Connection.Dispose();
            }
        }

        private static string NewSharedMemoryConnectionString() =>
            $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

        [Fact]
        public async Task DeleteCookingTask_ReferencedAsPrecedence_ReturnsBadRequest()
        {
            var connectionString = NewSharedMemoryConnectionString();
            using var db = new SqliteTestDb(connectionString);
            var controller = new CookingTasksController(db.Context);

            var createA = await controller.CreateCookingTask(
                new CookingTask { Name = "A", RequiredMachineId = 1, DurationMinutes = 10 });
            var taskA = (CookingTask)((CreatedAtActionResult)createA).Value!;

            await controller.CreateCookingTask(
                new CookingTask { Name = "B", RequiredMachineId = 2, DurationMinutes = 10, PrecedenceTaskId = taskA.Id });

            var deleteResult = await controller.DeleteCookingTask(taskA.Id);

            Assert.IsType<BadRequestObjectResult>(deleteResult);
        }
    }
}
