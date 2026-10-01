using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCookingTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CookingTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredMachineId = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecedenceTaskId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredIngredientId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequiredIngredientAmount = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CookingTasks_CookingTasks_PrecedenceTaskId",
                        column: x => x.PrecedenceTaskId,
                        principalTable: "CookingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CookingTasks_Machines_RequiredMachineId",
                        column: x => x.RequiredMachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsAvailable",
                value: true);

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsAvailable",
                value: true);

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsAvailable",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_CookingTasks_PrecedenceTaskId",
                table: "CookingTasks",
                column: "PrecedenceTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CookingTasks_RequiredMachineId",
                table: "CookingTasks",
                column: "RequiredMachineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookingTasks");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Machines");
        }
    }
}
