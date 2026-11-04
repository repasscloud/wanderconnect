using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountDetailControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "internal_note",
                table: "sanctions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                table: "sanctions",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "id_manual",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "photo_manual",
                table: "member_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "entitlements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revoked_reason",
                table: "entitlements",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_locks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    case_reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_reason = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_locks", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_locks_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verification_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    applied = table.Column<bool>(type: "boolean", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_verification_overrides_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_locks_account_id_created_at",
                table: "account_locks",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_verification_overrides_account_id_at",
                table: "verification_overrides",
                columns: new[] { "account_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_locks");

            migrationBuilder.DropTable(
                name: "verification_overrides");

            migrationBuilder.DropColumn(
                name: "internal_note",
                table: "sanctions");

            migrationBuilder.DropColumn(
                name: "reason_code",
                table: "sanctions");

            migrationBuilder.DropColumn(
                name: "id_manual",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "photo_manual",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "note",
                table: "entitlements");

            migrationBuilder.DropColumn(
                name: "revoked_reason",
                table: "entitlements");
        }
    }
}
