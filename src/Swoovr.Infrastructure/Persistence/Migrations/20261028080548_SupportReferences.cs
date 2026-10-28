using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "support_reference",
                table: "reports",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_support_reference",
                table: "reports",
                column: "support_reference",
                unique: true,
                filter: "support_reference IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_support_reference",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "support_reference",
                table: "reports");
        }
    }
}
