using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KitchenFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedRecipeData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Ingredients",
                columns: new[] { "Id", "Name", "StockQty", "Unit" },
                values: new object[,]
                {
                    { 1, "물", 10000m, "ml" },
                    { 2, "라면", 20m, "개" },
                    { 3, "양파", 3000m, "g" },
                    { 4, "당근", 2000m, "g" },
                    { 5, "양배추", 2000m, "g" },
                    { 6, "식용유", 1000m, "ml" },
                    { 7, "간장", 1000m, "ml" },
                    { 8, "닭", 5000m, "g" },
                    { 9, "버터", 500m, "g" },
                    { 10, "소금", 1000m, "g" },
                    { 11, "후추", 200m, "g" },
                    { 12, "마늘", 500m, "g" }
                });

            migrationBuilder.InsertData(
                table: "Machines",
                columns: new[] { "Id", "CapacityMl", "IsAvailable", "Kind", "MaxTempC", "MinTempC", "Name" },
                values: new object[,]
                {
                    { 4, 0, true, "", 0, 0, "인덕션" },
                    { 5, 0, true, "", 0, 0, "저울" }
                });

            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "라면" },
                    { 2, "야채볶음" },
                    { 3, "로스트치킨" }
                });

            migrationBuilder.InsertData(
                table: "Steps",
                columns: new[] { "Id", "Action", "DurationMinutes", "MachineId", "Order", "RecipeId", "TempC" },
                values: new object[,]
                {
                    { 1, "물 끓이기", 5, 4, 1, 1, 100 },
                    { 2, "면·스프 넣기", 1, 4, 2, 1, 100 },
                    { 3, "끓이기", 4, 4, 3, 1, 100 },
                    { 4, "채소 계량", 2, 5, 1, 2, null },
                    { 5, "양념 섞기", 2, 2, 2, 2, null },
                    { 6, "팬 예열", 3, 4, 3, 2, 180 },
                    { 7, "채소 볶기", 5, 4, 4, 2, 200 },
                    { 8, "양념 넣고 볶기", 2, 4, 5, 2, 200 },
                    { 9, "남은 재료 보관", 1, 3, 6, 2, 4 },
                    { 10, "오븐 예열", 10, 1, 1, 3, 200 },
                    { 11, "닭 꺼내기", 1, 3, 2, 3, 4 },
                    { 12, "닭 무게 재기", 1, 5, 3, 3, null },
                    { 13, "양념 계량", 2, 5, 4, 3, null },
                    { 14, "허브버터 만들기", 3, 2, 5, 3, null },
                    { 15, "1차 굽기", 30, 1, 6, 3, 200 },
                    { 16, "뒤집기", 1, 1, 7, 3, 200 },
                    { 17, "2차 굽기", 25, 1, 8, 3, 200 },
                    { 18, "버터 소스 끓이기", 5, 4, 9, 3, 120 },
                    { 19, "마무리 굽기", 10, 1, 10, 3, 230 },
                    { 20, "휴지", 10, 1, 11, 3, null },
                    { 21, "완성 무게 확인", 1, 5, 12, 3, null }
                });

            migrationBuilder.InsertData(
                table: "StepInputs",
                columns: new[] { "IngredientId", "StepId", "Quantity" },
                values: new object[,]
                {
                    { 1, 1, 550m },
                    { 2, 2, 1m },
                    { 3, 4, 150m },
                    { 4, 4, 100m },
                    { 5, 4, 200m },
                    { 7, 5, 30m },
                    { 6, 6, 20m },
                    { 8, 11, 1200m },
                    { 10, 13, 10m },
                    { 11, 13, 3m },
                    { 12, 13, 20m },
                    { 9, 14, 50m },
                    { 9, 18, 30m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 3, 4 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 4, 4 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 5, 4 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 7, 5 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 6, 6 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 8, 11 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 10, 13 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 11, 13 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 12, 13 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 9, 14 });

            migrationBuilder.DeleteData(
                table: "StepInputs",
                keyColumns: new[] { "IngredientId", "StepId" },
                keyValues: new object[] { 9, 18 });

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Ingredients",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Steps",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Machines",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Recipes",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
