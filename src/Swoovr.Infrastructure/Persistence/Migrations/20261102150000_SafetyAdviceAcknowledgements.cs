using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Swoovr.Infrastructure.Persistence.Migrations;
[DbContext(typeof(SwoovrDbContext))]
[Migration("20261102150000_SafetyAdviceAcknowledgements")]
public sealed class SafetyAdviceAcknowledgements:Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE safety_advice_acknowledgements (
            account_id uuid PRIMARY KEY REFERENCES accounts(id) ON DELETE CASCADE,
            revision character varying(64) NOT NULL,
            acknowledged_at timestamp with time zone NOT NULL
        );
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("DROP TABLE safety_advice_acknowledgements;");
}
