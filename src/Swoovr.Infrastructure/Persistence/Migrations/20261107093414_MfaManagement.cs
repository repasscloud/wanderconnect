using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MfaManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "mfa_last_reset_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mfa_required",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "mfa_requirement_role",
                table: "accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mfa_reset_pending",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "mfa_elevation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    initial_authentication_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    challenge_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    authenticated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mfa_elevation", x => x.id);
                    table.ForeignKey(
                        name: "fk_mfa_elevation_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mfa_elevation_account_id_session_digest_expires_at",
                table: "mfa_elevation",
                columns: new[] { "account_id", "session_digest", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mfa_elevation_challenge_digest",
                table: "mfa_elevation",
                column: "challenge_digest",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mfa_elevation");

            migrationBuilder.DropColumn(
                name: "mfa_last_reset_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "mfa_required",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "mfa_requirement_role",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "mfa_reset_pending",
                table: "accounts");
        }
    }
}
