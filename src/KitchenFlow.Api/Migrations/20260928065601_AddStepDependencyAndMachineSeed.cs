using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStepDependencyAndMachineSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StepDependencies",
                columns: table => new
                {
                    StepId = table.Column<int>(type: "INTEGER", nullable: false),
                    DependsOnStepId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepDependencies", x => new { x.StepId, x.DependsOnStepId });
                    table.ForeignKey(
                        name: "FK_StepDependencies_Steps_DependsOnStepId",
                        column: x => x.DependsOnStepId,
                        principalTable: "Steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StepDependencies_Steps_StepId",
                        column: x => x.StepId,
                        principalTable: "Steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 50000, "Oven", 250, 50 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC" },
                values: new object[] { 5000, "Mixer", 40 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 200000, "Fridge", 10, -20 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 5000, "Induction", 240, 30 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC" },
                values: new object[] { 10000, "Scale", 40 });

            migrationBuilder.InsertData(
                table: "StepDependencies",
                columns: new[] { "DependsOnStepId", "StepId" },
                values: new object[,]
                {
                    { 1, 2 },
                    { 2, 3 },
                    { 4, 7 },
                    { 6, 7 },
                    { 5, 8 },
                    { 7, 8 },
                    { 8, 9 },
                    { 11, 12 },
                    { 13, 14 },
                    { 10, 15 },
                    { 12, 15 },
                    { 14, 15 },
                    { 15, 16 },
                    { 16, 17 },
                    { 17, 19 },
                    { 18, 19 },
                    { 19, 20 },
                    { 20, 21 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StepDependencies_DependsOnStepId",
                table: "StepDependencies",
                column: "DependsOnStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StepDependencies");

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 0, "", 0, 0 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC" },
                values: new object[] { 0, "", 0 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 0, "", 0, 0 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 0, "", 0, 0 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC" },
                values: new object[] { 0, "", 0 });
        }
    }
}
