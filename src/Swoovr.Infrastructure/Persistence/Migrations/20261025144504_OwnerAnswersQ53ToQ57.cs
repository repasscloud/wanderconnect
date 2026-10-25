using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerAnswersQ53ToQ57 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "date_of_birth_verified",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "date_of_birth_verified_at",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            // Q57 (D161): "Prefer not to say" is a privacy choice, never a "show me" value; drop it from saved choices.
            migrationBuilder.Sql("UPDATE member_identities SET dating_show_me = array_remove(dating_show_me, 'unsaid') WHERE 'unsaid' = ANY(dating_show_me);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_birth_verified",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "date_of_birth_verified_at",
                table: "member_profiles");
        }
    }
}
