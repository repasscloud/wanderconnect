using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailRetirementReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_review_id",
                table: "email_retirement_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_retirement_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_retirement_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_retirement_reviews_email_retirement_operations_operat~",
                        column: x => x.operation_id,
                        principalTable: "email_retirement_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_retirement_reviews_operation_id_reviewed_at",
                table: "email_retirement_reviews",
                columns: new[] { "operation_id", "reviewed_at" });
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION email_retirement_guard() RETURNS trigger LANGUAGE plpgsql AS $$
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
                  IF NEW.current_review_id IS NOT NULL AND NOT EXISTS(
                    SELECT 1 FROM email_retirement_reviews r WHERE r.id=NEW.current_review_id AND r.operation_id=NEW.id
                      AND r.reviewed_at>=NEW.requested_at AND (NEW.completed_at IS NULL OR NEW.completed_at>=r.reviewed_at))
                  THEN RAISE EXCEPTION 'email retirement current approval is invalid'; END IF;
                  IF TG_OP='UPDATE' AND NEW.current_review_id IS DISTINCT FROM OLD.current_review_id THEN
                    IF OLD.state='Completed' OR NEW.state<>'Pending' OR NEW.current_review_id IS NULL
                      OR EXISTS(SELECT 1 FROM email_retirement_reviews previous JOIN email_retirement_reviews later
                        ON later.id=NEW.current_review_id WHERE previous.id=OLD.current_review_id AND later.reviewed_at<=previous.reviewed_at)
                    THEN RAISE EXCEPTION 'email retirement approval cannot move backwards'; END IF;
                  END IF;
                  IF NEW.state NOT IN ('Pending','NeedsReview','Completed') OR NEW.attempts<0 OR octet_length(NEW.encrypted_command)<29
                    OR (NEW.state='Completed') IS DISTINCT FROM (NEW.completed_at IS NOT NULL)
                  THEN RAISE EXCEPTION 'email retirement state is invalid'; END IF;
                  RETURN NEW;
                END $$;
                CREATE FUNCTION email_retirement_review_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('UPDATE','TRUNCATE') THEN RAISE EXCEPTION 'email retirement approval is immutable'; END IF;
                  IF TG_OP='DELETE' THEN
                    IF NOT email_retirement_delete_safe(OLD.operation_id) THEN RAISE EXCEPTION 'email retirement approval retention or preservation prevents deletion'; END IF;
                    RETURN OLD;
                  END IF;
                  IF NEW.actor_id='00000000-0000-0000-0000-000000000000'::uuid
                    OR NEW.access_grant_id='00000000-0000-0000-0000-000000000000'::uuid OR length(btrim(NEW.reason))=0
                    OR NOT EXISTS(SELECT 1 FROM email_retirement_operations o WHERE o.id=NEW.operation_id AND o.state<>'Completed' AND NEW.reviewed_at>=o.requested_at)
                  THEN RAISE EXCEPTION 'email retirement approval provenance is invalid'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER email_retirement_review_authority BEFORE INSERT OR UPDATE OR DELETE ON email_retirement_reviews
                  FOR EACH STATEMENT EXECUTE FUNCTION ban_disposal_authority();
                CREATE TRIGGER email_retirement_review_guard BEFORE INSERT OR UPDATE OR DELETE ON email_retirement_reviews
                  FOR EACH ROW EXECUTE FUNCTION email_retirement_review_guard();
                CREATE TRIGGER email_retirement_review_no_truncate BEFORE TRUNCATE ON email_retirement_reviews
                  FOR EACH STATEMENT EXECUTE FUNCTION email_retirement_review_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION email_retirement_guard() RETURNS trigger LANGUAGE plpgsql AS $$
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
                """);
            migrationBuilder.DropTable(
                name: "email_retirement_reviews");

            migrationBuilder.DropColumn(
                name: "current_review_id",
                table: "email_retirement_operations");
            migrationBuilder.Sql("DROP FUNCTION email_retirement_review_guard();");
        }
    }
}
