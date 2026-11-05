using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketingStaffMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticketing_staff",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_user_id = table.Column<long>(type: "bigint", nullable: true),
                    web_provider_user_id = table.Column<long>(type: "bigint", nullable: true),
                    token_protected = table.Column<byte[]>(type: "bytea", nullable: false),
                    applied_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    web_active = table.Column<bool>(type: "boolean", nullable: false),
                    sessions_revoked_applied = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_staff", x => x.account_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_staff_next_attempt_at",
                table: "ticketing_staff",
                column: "next_attempt_at");

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_staff_provider_user_id",
                table: "ticketing_staff",
                column: "provider_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_staff_web_provider_user_id",
                table: "ticketing_staff",
                column: "web_provider_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticketing_staff");
        }
    }
}
