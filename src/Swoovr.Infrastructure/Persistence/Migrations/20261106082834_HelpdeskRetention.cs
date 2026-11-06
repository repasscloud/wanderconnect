using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HelpdeskRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "customer_deleted_at",
                table: "ticketing_identity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "customer_deletion_task_id",
                table: "ticketing_identity",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_retirement_at",
                table: "ticketing_identity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retired_at",
                table: "ticketing_identity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retired_login",
                table: "ticketing_identity",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retirement_attempts",
                table: "ticketing_identity",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "retirement_failure",
                table: "ticketing_identity",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retirement_requested_at",
                table: "ticketing_identity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "helpdesk_ticket_retention",
                columns: table => new
                {
                    provider_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_customer_id = table.Column<long>(type: "bigint", nullable: false),
                    group_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    override_category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    override_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    overridden_by = table.Column<Guid>(type: "uuid", nullable: true),
                    overridden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    deletion_task_id = table.Column<long>(type: "bigint", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    customer_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_helpdesk_ticket_retention", x => x.provider_ticket_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_identity_next_retirement_at",
                table: "ticketing_identity",
                column: "next_retirement_at",
                filter: "retirement_requested_at IS NOT NULL AND retired_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_helpdesk_ticket_retention_provider_customer_id",
                table: "helpdesk_ticket_retention",
                column: "provider_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_helpdesk_ticket_retention_state_retain_until",
                table: "helpdesk_ticket_retention",
                columns: new[] { "state", "retain_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "helpdesk_ticket_retention");

            migrationBuilder.DropIndex(
                name: "ix_ticketing_identity_next_retirement_at",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "customer_deleted_at",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "customer_deletion_task_id",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "next_retirement_at",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "retired_at",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "retired_login",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "retirement_attempts",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "retirement_failure",
                table: "ticketing_identity");

            migrationBuilder.DropColumn(
                name: "retirement_requested_at",
                table: "ticketing_identity");
        }
    }
}
