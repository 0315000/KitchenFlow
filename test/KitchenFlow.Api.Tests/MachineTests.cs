using KitchenFlow.Api.Models;

namespace KitchenFlow.Api.Tests;

public class UnitTest1
{
    [Fact]  
    public void Machine_Name_Is_Set_Correctly()
    {
        var machine = new Machine { Id = 1, Name = "오븐" };

        Assert.Equal("오븐", machine.Name);
    }
}
