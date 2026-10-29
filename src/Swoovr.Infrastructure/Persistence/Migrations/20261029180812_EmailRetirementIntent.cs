using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailRetirementIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_retired_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_retirement_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    access_grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    encrypted_command = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_retirement_operations", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_retirement_operations_banned_identifiers_registry_id",
                        column: x => x.registry_id,
                        principalTable: "banned_identifiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_email_retirement_operations_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_retirement_operations_account_id",
                table: "email_retirement_operations",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_retirement_operations_case_id",
                table: "email_retirement_operations",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_retirement_operations_registry_id",
                table: "email_retirement_operations",
                column: "registry_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_retirement_operations_state_next_attempt_at",
                table: "email_retirement_operations",
                columns: new[] { "state", "next_attempt_at" });
            migrationBuilder.Sql("""
                CREATE FUNCTION email_retirement_delete_safe(source uuid) RETURNS boolean LANGUAGE sql VOLATILE AS $$
                  SELECT EXISTS(SELECT 1 FROM email_retirement_operations o WHERE o.id=source AND o.state='Completed'
                    AND o.completed_at IS NOT NULL
                    AND ((o.completed_at AT TIME ZONE 'UTC')+interval '24 months') AT TIME ZONE 'UTC' <= CURRENT_TIMESTAMP
                    AND ban_case_disposal_safe(o.case_id));
                $$;
                CREATE FUNCTION email_retirement_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='TRUNCATE' THEN RAISE EXCEPTION 'email retirement records cannot be truncated'; END IF;
                  IF TG_OP='DELETE' THEN
                    IF NOT email_retirement_delete_safe(OLD.id) THEN RAISE EXCEPTION 'email retirement retention or preservation prevents deletion'; END IF;
                    RETURN OLD;
                  END IF;
                  IF TG_OP='INSERT' AND NOT EXISTS(
                    SELECT 1 FROM accounts a JOIN moderation_cases c ON c.subject_account_id=a.id
                    JOIN banned_identifiers b ON b.case_id=c.id
                    WHERE a.id=NEW.account_id AND a.realm='Members' AND a.status='Active' AND a.email IS NULL AND a.email_retired_at IS NOT NULL
                      AND c.id=NEW.case_id AND b.id=NEW.registry_id AND b.identifier_type='Email'
                      AND b.status='Revoked' AND b.revoked_at IS NOT NULL AND b.revoked_by IS NOT NULL)
                  THEN RAISE EXCEPTION 'email retirement source provenance is invalid'; END IF;
                  IF TG_OP='UPDATE' THEN
                    IF (NEW.id,NEW.account_id,NEW.registry_id,NEW.case_id,NEW.requested_by,NEW.requested_at,NEW.access_grant_id)
                      IS DISTINCT FROM (OLD.id,OLD.account_id,OLD.registry_id,OLD.case_id,OLD.requested_by,OLD.requested_at,OLD.access_grant_id)
                      OR (OLD.state='Completed' AND (NEW.state,NEW.completed_at) IS DISTINCT FROM (OLD.state,OLD.completed_at))
                    THEN RAISE EXCEPTION 'email retirement provenance and completion are immutable'; END IF;
                  END IF;
                  IF NEW.state NOT IN ('Pending','NeedsReview','Completed') OR NEW.attempts<0 OR octet_length(NEW.encrypted_command)<29
                    OR (NEW.state='Completed') IS DISTINCT FROM (NEW.completed_at IS NOT NULL)
                  THEN RAISE EXCEPTION 'email retirement state is invalid'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER email_retirement_authority BEFORE INSERT OR UPDATE OR DELETE ON email_retirement_operations
                  FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER email_retirement_guard BEFORE INSERT OR UPDATE OR DELETE ON email_retirement_operations
                  FOR EACH ROW EXECUTE FUNCTION email_retirement_guard();
                CREATE TRIGGER email_retirement_no_truncate BEFORE TRUNCATE ON email_retirement_operations
                  FOR EACH STATEMENT EXECUTE FUNCTION email_retirement_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_retirement_operations");
            migrationBuilder.Sql("DROP FUNCTION email_retirement_guard(); DROP FUNCTION email_retirement_delete_safe(uuid);");

            migrationBuilder.DropColumn(
                name: "email_retired_at",
                table: "accounts");
        }
    }
}
