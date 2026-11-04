using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketingIntegrationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticketing_case_link",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appeal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_case_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticketing_identity",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_user_id = table.Column<long>(type: "bigint", nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_identity", x => x.account_id);
                });

            migrationBuilder.CreateTable(
                name: "ticketing_operation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_ticket_id = table.Column<long>(type: "bigint", nullable: true),
                    provider_article_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_operation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticketing_webhook_work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    body_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_article_id = table.Column<long>(type: "bigint", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_webhook_work", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_case_link_case_id",
                table: "ticketing_case_link",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_case_link_provider_ticket_id_case_id",
                table: "ticketing_case_link",
                columns: new[] { "provider_ticket_id", "case_id" },
                unique: true,
                filter: "appeal_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_case_link_provider_ticket_id_case_id_appeal_id",
                table: "ticketing_case_link",
                columns: new[] { "provider_ticket_id", "case_id", "appeal_id" },
                unique: true,
                filter: "appeal_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_identity_provider_user_id",
                table: "ticketing_identity",
                column: "provider_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_operation_account_id_created_at",
                table: "ticketing_operation",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_webhook_work_body_digest",
                table: "ticketing_webhook_work",
                column: "body_digest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_webhook_work_completed_at_next_attempt_at",
                table: "ticketing_webhook_work",
                columns: new[] { "completed_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_webhook_work_delivery_id",
                table: "ticketing_webhook_work",
                column: "delivery_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticketing_case_link");

            migrationBuilder.DropTable(
                name: "ticketing_identity");

            migrationBuilder.DropTable(
                name: "ticketing_operation");

            migrationBuilder.DropTable(
                name: "ticketing_webhook_work");
        }
    }
}
