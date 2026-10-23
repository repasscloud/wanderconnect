using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BadgesAndBots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "badge_definitions",
                columns: table => new
                {
                    badge = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badge_definitions", x => x.badge);
                });

            migrationBuilder.CreateTable(
                name: "bot_mode_authorisations",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authorised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    authorised_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bot_mode_authorisations", x => new { x.account_id, x.mode_id });
                    table.ForeignKey(
                        name: "fk_bot_mode_authorisations_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bot_mode_authorisations_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "badge_definitions",
                columns: new[] { "badge", "sort_order" },
                values: new object[,]
                {
                    { "Bot", 5 },
                    { "Founder", 1 },
                    { "IdVerified", 3 },
                    { "PhotoVerified", 2 },
                    { "Staff", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_bot_mode_authorisations_mode_id",
                table: "bot_mode_authorisations",
                column: "mode_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "badge_definitions");

            migrationBuilder.DropTable(
                name: "bot_mode_authorisations");
        }
    }
}
