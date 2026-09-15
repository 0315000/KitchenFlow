using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapacityMl",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Machines",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxTempC",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinTempC",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

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
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 0, "", 0, 0 });

            migrationBuilder.UpdateData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CapacityMl", "Kind", "MaxTempC", "MinTempC" },
                values: new object[] { 0, "", 0, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapacityMl",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "MaxTempC",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "MinTempC",
                table: "Machines");
        }
    }
}
