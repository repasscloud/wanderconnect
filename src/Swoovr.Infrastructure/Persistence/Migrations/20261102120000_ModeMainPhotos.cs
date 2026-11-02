using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Swoovr.Infrastructure.Persistence.Migrations;

public partial class ModeMainPhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // D293: preserve existing rows/objects but never infer a main selection from the legacy shared main.
        migrationBuilder.AddColumn<bool>(name: "is_main", table: "media_placements", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.CreateIndex(name: "ix_media_placements_one_main", table: "media_placements", columns: new[] { "account_id", "scope" }, unique: true, filter: "is_main = true");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_media_placements_one_main", table: "media_placements");
        migrationBuilder.DropColumn(name: "is_main", table: "media_placements");
    }
}
