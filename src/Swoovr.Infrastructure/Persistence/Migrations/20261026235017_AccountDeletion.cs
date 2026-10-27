using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "system_flag",
                table: "moderation_cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "delete_founder_token",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_due_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_requested_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deletion_restore_status",
                table: "accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "data_exports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    downloaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_exports", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_exports_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terminated_email_holds",
                columns: table => new
                {
                    email_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terminated_email_holds", x => x.email_hash);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_status_deletion_due_at",
                table: "accounts",
                columns: new[] { "status", "deletion_due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_account_id_created_at",
                table: "data_exports",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_token_hash",
                table: "data_exports",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terminated_email_holds_expires_at",
                table: "terminated_email_holds",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_exports");

            migrationBuilder.DropTable(
                name: "terminated_email_holds");

            migrationBuilder.DropIndex(
                name: "ix_accounts_status_deletion_due_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "system_flag",
                table: "moderation_cases");

            migrationBuilder.DropColumn(
                name: "delete_founder_token",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "deletion_due_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "deletion_requested_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "deletion_restore_status",
                table: "accounts");
        }
    }
}
