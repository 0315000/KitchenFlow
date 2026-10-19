using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOvenPreheat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StepDependencies",
                keyColumns: new[] { "DependsOnStepId", "StepId" },
                keyValues: new object[] { 10, 15 });

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreheat",
                table: "RunEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPreheat",
                table: "RunEvents");

            migrationBuilder.InsertData(
                table: "Steps",
                columns: new[] { "Id", "Action", "DurationMinutes", "MachineId", "Order", "RecipeId", "TempC" },
                values: new object[] { 10, "오븐 예열", 10, 1, 1, 3, 200 });

            migrationBuilder.InsertData(
                table: "StepDependencies",
                columns: new[] { "DependsOnStepId", "StepId" },
                values: new object[] { 10, 15 });
        }
    }
}
