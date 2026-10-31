using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BetaInvitationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_attempted_at",
                table: "beta_enrolments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "invitation_attempts",
                table: "beta_enrolments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_completed_at",
                table: "beta_enrolments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_next_attempt_at",
                table: "beta_enrolments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invitation_outcome_code",
                table: "beta_enrolments",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_queued_at",
                table: "beta_enrolments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invitation_state",
                table: "beta_enrolments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "not-queued");

            migrationBuilder.CreateIndex(
                name: "ix_beta_enrolments_invitation_state_invitation_next_attempt_at",
                table: "beta_enrolments",
                columns: new[] { "invitation_state", "invitation_next_attempt_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_beta_invitation",
                table: "beta_enrolments",
                sql: "invitation_state IN ('not-queued','pending','dispatching','accepted','rejected','unknown','cancelled','expired') AND invitation_attempts BETWEEN 0 AND 3 AND ((invitation_state='not-queued' AND invitation_queued_at IS NULL AND invitation_attempts=0) OR (invitation_state<>'not-queued' AND invitation_queued_at IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_beta_enrolments_invitation_state_invitation_next_attempt_at",
                table: "beta_enrolments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_beta_invitation",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_attempted_at",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_attempts",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_completed_at",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_next_attempt_at",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_outcome_code",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_queued_at",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "invitation_state",
                table: "beta_enrolments");
        }
    }
}
