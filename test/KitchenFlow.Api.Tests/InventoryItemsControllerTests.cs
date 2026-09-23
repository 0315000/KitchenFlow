using KitchenFlow.Api.Controllers;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KitchenFlow.Api.Tests;

public class InventoryItemsControllerTests
{
    // ---- 일반 검증 테스트: EF Core InMemory provider 사용 (빠르고 단순) ----
    // 주의: InMemory provider는 ExecuteUpdateAsync(원자적 UPDATE)를 지원하지 않으므로,
    // ConsumeInventoryItem이 실제로 차감을 수행하는 경로는 아래쪽 SQLite 기반 테스트에서 검증한다.

    private static KitchenFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new KitchenFlowDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static InventoryItem ValidItem(string name = "설탕") => new()
    {
        ItemName = name,
        Quantity = 10,
        ExpiryDate = DateTime.Today.AddDays(7),
        Threshold = 2,
    };

    [Fact]
    public async Task CreateInventoryItem_EmptyItemName_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var item = ValidItem();
        item.ItemName = "   ";

        var result = await controller.CreateInventoryItem(item);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("재료명", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateInventoryItem_ItemNameTooLong_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var item = ValidItem(new string('가', 101));

        var result = await controller.CreateInventoryItem(item);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("100자", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateInventoryItem_NegativeQuantity_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var item = ValidItem();
        item.Quantity = -1;

        var result = await controller.CreateInventoryItem(item);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("수량", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateInventoryItem_PastExpiryDate_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var item = ValidItem();
        item.ExpiryDate = DateTime.Today.AddDays(-1);

        var result = await controller.CreateInventoryItem(item);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("유통기한", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateInventoryItem_ValidData_ReturnsCreated()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);

        var result = await controller.CreateInventoryItem(ValidItem());

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task GetInventoryItem_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);

        var result = await controller.GetInventoryItem(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 재료를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task UpdateInventoryItem_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var item = ValidItem();
        item.Id = 9999;

        var result = await controller.UpdateInventoryItem(9999, item);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 재료를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task DeleteInventoryItem_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);

        var result = await controller.DeleteInventoryItem(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 재료를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task UpdateInventoryItem_PastExpiryDate_IsAllowed()
    {
        // 신규 등록과 달리, 이미 등록된 재고를 수정할 때는 과거 유통기한을 허용한다.
        // (예: 유통기한이 지난 재고의 수량만 정정하는 등 정상적인 수정 시나리오를 막지 않기 위함)
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        var created = await controller.CreateInventoryItem(ValidItem("우유"));
        var item = (InventoryItem)((CreatedAtActionResult)created).Value!;

        item.ExpiryDate = DateTime.Today.AddDays(-3);
        var result = await controller.UpdateInventoryItem(item.Id, item);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ConsumeInventoryItem_NonPositiveAmount_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);

        var result = await controller.ConsumeInventoryItem(1, new ConsumeInventoryRequest { Amount = 0 });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("차감 수량", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task ConsumeInventoryItem_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);

        var result = await controller.ConsumeInventoryItem(9999, new ConsumeInventoryRequest { Amount = 1 });

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 재료를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task ConsumeInventoryItem_ExpiredItem_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new InventoryItemsController(context);
        context.InventoryItems.Add(new InventoryItem
        {
            Id = 1,
            ItemName = "상한우유",
            Quantity = 5,
            ExpiryDate = DateTime.Today.AddDays(-1),
            Threshold = 1,
        });
        await context.SaveChangesAsync();

        var result = await controller.ConsumeInventoryItem(1, new ConsumeInventoryRequest { Amount = 1 });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("유통기한", badRequest.Value!.ToString());
    }

    // ---- 동시성(몽키테스트) 테스트: 실제 원자적 UPDATE를 검증하려면 관계형 SQLite provider가 필요하다.
    // "cache=shared" 메모리 DB + 여러 개의 별도 SqliteConnection을 사용해 여러 요청이
    // 동시에 같은 재료를 차감하는 상황을 재현한다. ----

    private sealed class SqliteTestDb : IDisposable
    {
        public SqliteConnection Connection { get; }
        public KitchenFlowDbContext Context { get; }

        public SqliteTestDb(string connectionString)
        {
            Connection = new SqliteConnection(connectionString);
            Connection.Open();
            var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
                .UseSqlite(Connection)
                .Options;
            Context = new KitchenFlowDbContext(options);
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
    public async Task ConsumeInventoryItem_SufficientStock_DecrementsQuantity()
    {
        var connectionString = NewSharedMemoryConnectionString();
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open(); // 이 연결이 열려 있는 동안만 공유 메모리 DB가 유지된다.

        using (var setup = new SqliteTestDb(connectionString))
        {
            setup.Context.Database.EnsureCreated();
            setup.Context.InventoryItems.Add(new InventoryItem
            {
                ItemName = "밀가루",
                Quantity = 5,
                ExpiryDate = DateTime.Today.AddDays(10),
                Threshold = 1,
            });
            await setup.Context.SaveChangesAsync();
        }

        using var db = new SqliteTestDb(connectionString);
        var controller = new InventoryItemsController(db.Context);
        var result = await controller.ConsumeInventoryItem(1, new ConsumeInventoryRequest { Amount = 3 });

        Assert.IsType<NoContentResult>(result);

        using var verify = new SqliteTestDb(connectionString);
        var item = await verify.Context.InventoryItems.FindAsync(1);
        Assert.Equal(2, item!.Quantity);
    }

    [Fact]
    public async Task ConsumeInventoryItem_InsufficientStock_ReturnsBadRequestAndLeavesQuantityUnchanged()
    {
        var connectionString = NewSharedMemoryConnectionString();
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        using (var setup = new SqliteTestDb(connectionString))
        {
            setup.Context.Database.EnsureCreated();
            setup.Context.InventoryItems.Add(new InventoryItem
            {
                ItemName = "버터",
                Quantity = 2,
                ExpiryDate = DateTime.Today.AddDays(10),
                Threshold = 1,
            });
            await setup.Context.SaveChangesAsync();
        }

        using var db = new SqliteTestDb(connectionString);
        var controller = new InventoryItemsController(db.Context);
        var result = await controller.ConsumeInventoryItem(1, new ConsumeInventoryRequest { Amount = 5 });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("재고가 부족", badRequest.Value!.ToString());

        using var verify = new SqliteTestDb(connectionString);
        var item = await verify.Context.InventoryItems.FindAsync(1);
        Assert.Equal(2, item!.Quantity); // 실패한 요청은 재고를 전혀 바꾸지 않는다.
    }

    [Fact]
    public async Task AdjustInventoryItem_InsufficientStock_ReturnsBadRequestAndLeavesQuantityUnchanged()
    {
        var connectionString = NewSharedMemoryConnectionString();
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        using (var setup = new SqliteTestDb(connectionString))
        {
            setup.Context.Database.EnsureCreated();
            setup.Context.InventoryItems.Add(new InventoryItem
            {
                ItemName = "소금", Quantity = 3, ExpiryDate = DateTime.Today.AddDays(10), Threshold = 1,
            });
            await setup.Context.SaveChangesAsync();
        }

        using var db = new SqliteTestDb(connectionString);
        var controller = new InventoryItemsController(db.Context);
        var result = await controller.AdjustInventoryItem(1, new AdjustInventoryRequest { Amount = -5 });

        Assert.IsType<BadRequestObjectResult>(result);

        using var verify = new SqliteTestDb(connectionString);
        var item = await verify.Context.InventoryItems.FindAsync(1);
        Assert.Equal(3, item!.Quantity);
    }

    [Fact]
    public async Task ConsumeInventoryItem_ConcurrentRequests_NeverGoesNegative()
    {
        // 몽키테스트/동시성 완료 조건 재현:
        // 재고가 10개뿐인데 20개의 "사용" 요청이 거의 동시에 들어온다.
        // 정확히 10건만 성공해야 하고, 최종 재고는 절대 음수가 되어서는 안 된다.
        var connectionString = NewSharedMemoryConnectionString();
        using var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        using (var setup = new SqliteTestDb(connectionString))
        {
            setup.Context.Database.EnsureCreated();
            setup.Context.InventoryItems.Add(new InventoryItem
            {
                ItemName = "계란",
                Quantity = 10,
                ExpiryDate = DateTime.Today.AddDays(10),
                Threshold = 1,
            });
            await setup.Context.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var db = new SqliteTestDb(connectionString);
            var controller = new InventoryItemsController(db.Context);
            return await controller.ConsumeInventoryItem(1, new ConsumeInventoryRequest { Amount = 1 });
        });

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r is NoContentResult);
        var failureCount = results.Count(r => r is BadRequestObjectResult);

        Assert.Equal(10, successCount);
        Assert.Equal(10, failureCount);

        using var verify = new SqliteTestDb(connectionString);
        var item = await verify.Context.InventoryItems.FindAsync(1);
        Assert.Equal(0, item!.Quantity); // 절대 음수가 되지 않고 정확히 0에서 멈춘다.
    }
}
