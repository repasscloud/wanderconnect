using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionRequestsAndBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocks",
                columns: table => new
                {
                    blocker_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocks", x => new { x.blocker_account_id, x.blocked_account_id });
                    table.CheckConstraint("ck_blocks_not_self", "blocker_account_id <> blocked_account_id");
                    table.ForeignKey(
                        name: "fk_blocks_accounts_blocked_account_id",
                        column: x => x.blocked_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blocks_accounts_blocker_account_id",
                        column: x => x.blocker_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connection_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_low = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_high = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connection_requests", x => x.id);
                    table.CheckConstraint("ck_connection_requests_not_self", "from_account_id <> to_account_id");
                    table.ForeignKey(
                        name: "fk_connection_requests_accounts_from_account_id",
                        column: x => x.from_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_connection_requests_accounts_to_account_id",
                        column: x => x.to_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_connection_requests_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocked_account_id",
                table: "blocks",
                column: "blocked_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_from_account_id_created_at",
                table: "connection_requests",
                columns: new[] { "from_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_mode_id",
                table: "connection_requests",
                column: "mode_id");

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_one_accepted",
                table: "connection_requests",
                columns: new[] { "pair_low", "pair_high", "mode_id" },
                unique: true,
                filter: "status = 'Accepted'");

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_one_pending",
                table: "connection_requests",
                columns: new[] { "pair_low", "pair_high", "mode_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_to_account_id_status",
                table: "connection_requests",
                columns: new[] { "to_account_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocks");

            migrationBuilder.DropTable(
                name: "connection_requests");
        }
    }
}
