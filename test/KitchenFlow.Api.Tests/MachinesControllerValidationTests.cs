using KitchenFlow.Api.Controllers;
using KitchenFlow.Api.Data;
using KitchenFlow.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KitchenFlow.Api.Tests;

public class MachinesControllerValidationTests
{
    private static KitchenFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KitchenFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new KitchenFlowDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Machine ValidMachine(string name = "제빵기") => new()
    {
        Name = name,
        Kind = "Oven",
        CapacityMl = 1000,
        MinTempC = 50,
        MaxTempC = 250,
    };

    // ---- DP3-1 필드별 검증: 잘못된 값 4가지 ----

    [Fact]
    public async Task CreateMachine_EmptyName_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine();
        machine.Name = "   ";

        var result = await controller.CreateMachine(machine);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("이름", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateMachine_NameTooLong_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine(new string('가', 101));

        var result = await controller.CreateMachine(machine);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("100자", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateMachine_NameWithLeadingOrTrailingWhitespace_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine(" 제빵기");

        var result = await controller.CreateMachine(machine);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateMachine_DisallowedKind_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine();
        machine.Kind = "Toaster"; // 허용 목록(Oven/Mixer/Fridge)에 없음

        var result = await controller.CreateMachine(machine);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("종류", badRequest.Value!.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateMachine_CapacityNotPositive_ReturnsBadRequest(int capacity)
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine();
        machine.CapacityMl = capacity;

        var result = await controller.CreateMachine(machine);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("용량", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateMachine_MinTempGreaterThanMaxTemp_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine();
        machine.MinTempC = 200;
        machine.MaxTempC = 100;

        var result = await controller.CreateMachine(machine);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("최저온도", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateMachine_DuplicateName_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        await controller.CreateMachine(ValidMachine("에어프라이어"));

        var result = await controller.CreateMachine(ValidMachine("에어프라이어"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("이미", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task CreateMachine_ValidData_ReturnsCreated()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);

        var result = await controller.CreateMachine(ValidMachine("정상기계"));

        Assert.IsType<CreatedAtActionResult>(result);
    }

    // ---- 서로 다른 사유 메시지 검증 (완료 조건: 4가지 잘못된 값 → 서로 다른 메시지) ----

    [Fact]
    public async Task CreateMachine_FourInvalidCases_ProduceDifferentMessages()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);

        var nameError = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateMachine(ValidMachine(""))).Value!.ToString();

        var kindMachine = ValidMachine("기계B");
        kindMachine.Kind = "Toaster";
        var kindError = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateMachine(kindMachine)).Value!.ToString();

        var capacityMachine = ValidMachine("기계C");
        capacityMachine.CapacityMl = 0;
        var capacityError = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateMachine(capacityMachine)).Value!.ToString();

        var tempMachine = ValidMachine("기계D");
        tempMachine.MinTempC = 300;
        tempMachine.MaxTempC = 100;
        var tempError = Assert.IsType<BadRequestObjectResult>(
            await controller.CreateMachine(tempMachine)).Value!.ToString();

        var messages = new[] { nameError, kindError, capacityError, tempError };
        Assert.Equal(messages.Length, messages.Distinct().Count());
    }

    // ---- 404: 존재하지 않는 ID ----

    [Fact]
    public async Task GetMachine_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);

        var result = await controller.GetMachine(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 기계를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task UpdateMachine_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var machine = ValidMachine();
        machine.Id = 9999;

        var result = await controller.UpdateMachine(9999, machine);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 기계를 찾을 수 없습니다.", notFound.Value);
    }

    [Fact]
    public async Task DeleteMachine_UnknownId_ReturnsNotFoundWithMessage()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);

        var result = await controller.DeleteMachine(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("해당 기계를 찾을 수 없습니다.", notFound.Value);
    }

    // ---- 수정 시 중복 이름 검증 (자기 자신은 제외, 다른 기계와는 겹치면 거부) ----

    [Fact]
    public async Task UpdateMachine_RenamedToAnotherExistingMachinesName_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        await controller.CreateMachine(ValidMachine("기계A"));
        var createdB = await controller.CreateMachine(ValidMachine("기계B"));
        var machineB = (Machine)((CreatedAtActionResult)createdB).Value!;

        machineB.Name = "기계A";
        var result = await controller.UpdateMachine(machineB.Id, machineB);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("이미", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task UpdateMachine_KeepingOwnName_Succeeds()
    {
        using var context = CreateContext();
        var controller = new MachinesController(context);
        var created = await controller.CreateMachine(ValidMachine("기계X"));
        var machine = (Machine)((CreatedAtActionResult)created).Value!;

        machine.CapacityMl = 2000; // 이름은 그대로, 다른 필드만 변경
        var result = await controller.UpdateMachine(machine.Id, machine);

        Assert.IsType<NoContentResult>(result);
    }
}
