using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D350 (#277): Area is removed from the profile. Owner-approved exception to the add-only migration rule
    /// (TESTING.md); current data is fictional. Down re-adds an empty nullable column.
    /// </summary>
    public partial class RemoveProfileArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "area",
                table: "member_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "member_profiles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }
    }
}
