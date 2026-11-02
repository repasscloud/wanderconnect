using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations;

public partial class DobChangeGrants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing populated dates lock by presence; do not rewrite or clear any member date.
        migrationBuilder.Sql("""
            CREATE TABLE restricted.dob_change_grants (
                id uuid CONSTRAINT pk_dob_change_grants PRIMARY KEY, account_id uuid NOT NULL, granted_by uuid NOT NULL,
                reason varchar(500) NOT NULL, created_at timestamptz NOT NULL, expires_at timestamptz NOT NULL,
                consumed_at timestamptz NULL, revoked_at timestamptz NULL, revoked_by uuid NULL,
                CONSTRAINT ck_dob_grant_window CHECK (expires_at = created_at + interval '24 hours')
            );
            CREATE INDEX ix_dob_change_grants_account_id_created_at ON restricted.dob_change_grants(account_id,created_at);
            CREATE UNIQUE INDEX ix_dob_change_grants_account_id ON restricted.dob_change_grants(account_id)
                WHERE consumed_at IS NULL AND revoked_at IS NULL;
            CREATE TABLE restricted.dob_change_histories (
                id uuid CONSTRAINT pk_dob_change_histories PRIMARY KEY, account_id uuid NOT NULL, grant_id uuid NULL, sealed bytea NOT NULL,
                changed_at timestamptz NOT NULL, retain_until timestamptz NULL
            );
            CREATE INDEX ix_dob_change_histories_account_id_changed_at ON restricted.dob_change_histories(account_id,changed_at);
            CREATE INDEX ix_dob_change_histories_retain_until ON restricted.dob_change_histories(retain_until);
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name:"dob_change_histories",schema:"restricted");
        migrationBuilder.DropTable(name:"dob_change_grants",schema:"restricted");
    }
}
