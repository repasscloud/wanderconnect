using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Swoovr.Infrastructure.Persistence.Migrations;

// Authority is deliberately an unmapped singleton: accessed through revisioned SQL, never SaveChanges upserts.
[DbContext(typeof(SwoovrDbContext))]
[Migration("20261102110000_RegistrationAvailability")]
public sealed class RegistrationAvailability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE registration_availability (
            id integer PRIMARY KEY CHECK(id=1), beta_enabled boolean NOT NULL, public_enabled boolean NOT NULL,
            revision bigint NOT NULL CHECK(revision>=0), changed_at timestamp with time zone NOT NULL,
            changed_by uuid NULL, reason character varying(500) NOT NULL
        );
        INSERT INTO registration_availability VALUES(1,true,false,0,CURRENT_TIMESTAMP,NULL,'D295 UAT begins invite-only');
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("DROP TABLE registration_availability;");
}
