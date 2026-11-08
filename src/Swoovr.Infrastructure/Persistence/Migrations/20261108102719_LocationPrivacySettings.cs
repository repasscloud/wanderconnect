using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D348 (#276): "Appear in nearby searches" and "Show my approximate distance", both off for new and existing members.
    /// Non-null with a database default of false, so hosts still on older code keep inserting profiles unchanged.
    /// </summary>
    public partial class LocationPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "appear_in_nearby",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "show_distance",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "appear_in_nearby",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "show_distance",
                table: "member_profiles");
        }
    }
}
