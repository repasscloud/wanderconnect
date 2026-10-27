using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeriousIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "police",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "police_reference",
                table: "reports",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "serious",
                table: "report_categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "fake",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "harassment",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "hate",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "inappropriate",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "other",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "scam",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "sexual",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "spam",
                column: "serious",
                value: false);

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "threats",
                columns: new[] { "label", "serious" },
                values: new object[] { "Threats to my safety", true });

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "underage",
                column: "serious",
                value: false);

            migrationBuilder.InsertData(
                table: "report_categories",
                columns: new[] { "id", "available", "label", "priority", "serious", "sort_order" },
                values: new object[,]
                {
                    { "child-exploitation", true, "Child sexual exploitation or a suspected minor", "Urgent", true, 15 },
                    { "coercion", true, "Coercion, extortion or sextortion", "Urgent", true, 14 },
                    { "danger", true, "Other immediate danger", "Urgent", true, 16 },
                    { "sexual-assault", true, "Sexual assault", "Urgent", true, 11 },
                    { "stalking", true, "Stalking or being followed", "Urgent", true, 13 },
                    { "violence", true, "Physical violence", "Urgent", true, 12 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "child-exploitation");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "coercion");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "danger");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "sexual-assault");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "stalking");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "violence");

            migrationBuilder.DropColumn(
                name: "police",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "police_reference",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "serious",
                table: "report_categories");

            migrationBuilder.UpdateData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: "threats",
                column: "label",
                value: "Threats or violence");
        }
    }
}
