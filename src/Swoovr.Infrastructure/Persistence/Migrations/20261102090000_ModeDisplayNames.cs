using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations;

public partial class ModeDisplayNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing aliases remain unset. Never copy registered/provider names or rewrite historical snapshots.
        migrationBuilder.AddColumn<string>(name: "display_name", table: "member_modes", type: "character varying(40)", maxLength: 40, nullable: true);
        // Historical conversations have no trustworthy originating mode: preserve unknown, use main public name.
        migrationBuilder.AddColumn<string>(name: "origin_mode_id", table: "conversations", type: "character varying(32)", maxLength: 32, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "display_name", table: "member_modes");
        migrationBuilder.DropColumn(name: "origin_mode_id", table: "conversations");
    }
}
