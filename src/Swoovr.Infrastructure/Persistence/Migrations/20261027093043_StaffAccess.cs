using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sessions_revoked_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "access_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    renewals = table.Column<int>(type: "integer", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_grants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "export_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_export_approvals_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensitive_access_log",
                columns: table => new
                {
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    resource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    previous_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensitive_access_log", x => x.sequence);
                });

            migrationBuilder.CreateTable(
                name: "staff_exclusions",
                columns: table => new
                {
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_exclusions", x => new { x.staff_account_id, x.subject_account_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_grants_account_id_capability_expires_at",
                table: "access_grants",
                columns: new[] { "account_id", "capability", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_export_approvals_hold_id",
                table: "export_approvals",
                column: "hold_id");

            migrationBuilder.CreateIndex(
                name: "ix_sensitive_access_log_actor_account_id_at",
                table: "sensitive_access_log",
                columns: new[] { "actor_account_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_sensitive_access_log_resource_resource_id",
                table: "sensitive_access_log",
                columns: new[] { "resource", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_exclusions_subject_account_id",
                table: "staff_exclusions",
                column: "subject_account_id");

            // #88 (policy §11, §15): the sensitive-access log is append-only in the database itself.
            migrationBuilder.Sql("""
                CREATE FUNCTION sensitive_access_log_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'sensitive_access_log is append-only'; END $$;
                CREATE TRIGGER sensitive_access_log_no_change BEFORE UPDATE OR DELETE ON sensitive_access_log
                    FOR EACH ROW EXECUTE FUNCTION sensitive_access_log_append_only();
                CREATE TRIGGER sensitive_access_log_no_truncate BEFORE TRUNCATE ON sensitive_access_log
                    FOR EACH STATEMENT EXECUTE FUNCTION sensitive_access_log_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS sensitive_access_log_no_truncate ON sensitive_access_log;
                DROP TRIGGER IF EXISTS sensitive_access_log_no_change ON sensitive_access_log;
                DROP FUNCTION IF EXISTS sensitive_access_log_append_only();
                """);

            migrationBuilder.DropTable(
                name: "access_grants");

            migrationBuilder.DropTable(
                name: "export_approvals");

            migrationBuilder.DropTable(
                name: "sensitive_access_log");

            migrationBuilder.DropTable(
                name: "staff_exclusions");

            migrationBuilder.DropColumn(
                name: "sessions_revoked_at",
                table: "accounts");
        }
    }
}
