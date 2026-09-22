using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitToInventoryItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "InventoryItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "InventoryItems");
        }
    }
}
