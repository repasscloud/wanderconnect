using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Swoovr.Infrastructure.Persistence.Migrations;
[DbContext(typeof(SwoovrDbContext))]
[Migration("20261102170000_MediaActionReceipts")]
public sealed class MediaActionReceipts:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DROP INDEX ix_moderation_transparency_facts_source_kind_source_id;
        CREATE UNIQUE INDEX ix_transparency_source_action
            ON moderation_transparency_facts(source_kind,source_id,action_at);
        """);
    // Refuse rollback if multiple actions would lose their unique source identity; never remove receipts.
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE UNIQUE INDEX ix_moderation_transparency_facts_source_kind_source_id
            ON moderation_transparency_facts(source_kind,source_id);
        DROP INDEX ix_transparency_source_action;
        """);
}
