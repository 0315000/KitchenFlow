using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Machines_Name",
                table: "Machines",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Machines_Name",
                table: "Machines");
        }
    }
}
