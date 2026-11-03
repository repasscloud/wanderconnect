using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotoReviewWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_retain_until",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_decision_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_decision_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_decision_records_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_safety_acknowledgements",
                columns: table => new
                {
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_safety_acknowledgements", x => new { x.staff_account_id, x.version });
                    table.ForeignKey(
                        name: "fk_media_safety_acknowledgements_accounts_staff_account_id",
                        column: x => x.staff_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_appeals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_appeals", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_appeals_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_media_appeals_media_decision_records_decision_id",
                        column: x => x.decision_id,
                        principalTable: "media_decision_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_appeals_account_id",
                table: "media_appeals",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_appeals_decided_at_submitted_at",
                table: "media_appeals",
                columns: new[] { "decided_at", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_appeals_decision_id",
                table: "media_appeals",
                column: "decision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_decision_records_account_id_at",
                table: "media_decision_records",
                columns: new[] { "account_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_decision_records_media_id_at",
                table: "media_decision_records",
                columns: new[] { "media_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_appeals");

            migrationBuilder.DropTable(
                name: "media_safety_acknowledgements");

            migrationBuilder.DropTable(
                name: "media_decision_records");

            migrationBuilder.DropColumn(
                name: "rejected_retain_until",
                table: "media_items");
        }
    }
}
