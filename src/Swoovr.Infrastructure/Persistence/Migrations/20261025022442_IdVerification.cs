using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_founder_places_place",
                table: "founder_places");

            migrationBuilder.AddColumn<bool>(
                name: "can_request_id_verification",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                // D128: every existing member starts with their one self-service attempt.
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "founder_eligible_at",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "founder_entitlement_from",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "didit_webhook_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    webhook_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_didit_webhook_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "id_verification_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started = table.Column<bool>(type: "boolean", nullable: false),
                    self_service = table.Column<bool>(type: "boolean", nullable: false),
                    adult = table.Column<bool>(type: "boolean", nullable: true),
                    url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_id_verification_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_id_verification_attempts_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_founder_eligible_at",
                table: "member_profiles",
                column: "founder_eligible_at",
                filter: "founder_eligible_at IS NOT NULL AND founder_place IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_founder_places_place",
                table: "founder_places",
                sql: "place >= 1 AND place <= 1000");

            migrationBuilder.CreateIndex(
                name: "ix_id_verification_attempts_one_open",
                table: "id_verification_attempts",
                column: "account_id",
                unique: true,
                filter: "state IN ('Created', 'InProgress', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "ix_id_verification_attempts_session_id",
                table: "id_verification_attempts",
                column: "session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "didit_webhook_events");

            migrationBuilder.DropTable(
                name: "id_verification_attempts");

            migrationBuilder.DropIndex(
                name: "ix_member_profiles_founder_eligible_at",
                table: "member_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_founder_places_place",
                table: "founder_places");

            migrationBuilder.DropColumn(
                name: "can_request_id_verification",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "founder_eligible_at",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "founder_entitlement_from",
                table: "member_profiles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_founder_places_place",
                table: "founder_places",
                sql: "place >= 1");
        }
    }
}
