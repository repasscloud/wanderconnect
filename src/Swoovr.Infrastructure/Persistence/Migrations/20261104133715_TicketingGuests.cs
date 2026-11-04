using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketingGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticketing_guest",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_lookup = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    email_protected = table.Column<byte[]>(type: "bytea", nullable: false),
                    provider_user_id = table.Column<long>(type: "bigint", nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_guest", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticketing_guest_challenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_lookup = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    address_lookup = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_guest_challenge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticketing_guest_grant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticketing_guest_grant", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticketing_guest_grant_ticketing_guest_guest_id",
                        column: x => x.guest_id,
                        principalTable: "ticketing_guest",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_email_lookup",
                table: "ticketing_guest",
                column: "email_lookup",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_provider_user_id",
                table: "ticketing_guest",
                column: "provider_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_challenge_address_lookup_created_at",
                table: "ticketing_guest_challenge",
                columns: new[] { "address_lookup", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_challenge_email_lookup_created_at",
                table: "ticketing_guest_challenge",
                columns: new[] { "email_lookup", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_grant_guest_id_expires_at",
                table: "ticketing_guest_grant",
                columns: new[] { "guest_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticketing_guest_grant_token_hash",
                table: "ticketing_guest_grant",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticketing_guest_challenge");

            migrationBuilder.DropTable(
                name: "ticketing_guest_grant");

            migrationBuilder.DropTable(
                name: "ticketing_guest");
        }
    }
}
