using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaPreservationRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_preservation_at",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preservation_attempts",
                table: "media_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preservation_completed_at",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preservation_error",
                table: "media_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "preservation_legacy",
                table: "media_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preservation_pending_at",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preservation_source_key",
                table: "media_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_escalation_deliveries",
                columns: table => new
                {
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_actor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_escalation_deliveries", x => new { x.media_id, x.slot });
                    table.ForeignKey(
                        name: "fk_media_escalation_deliveries_media_items_media_id",
                        column: x => x.media_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_escalation_deliveries");

            migrationBuilder.DropColumn(
                name: "next_preservation_at",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_attempts",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_completed_at",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_error",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_legacy",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_pending_at",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "preservation_source_key",
                table: "media_items");
        }
    }
}
