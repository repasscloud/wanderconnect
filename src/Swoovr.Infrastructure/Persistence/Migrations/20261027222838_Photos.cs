using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Photos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    upload_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    bytes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    screen_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_screen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_screen_error = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    screened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    screener = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    policy_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    signals = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    known_content_checked = table.Column<bool>(type: "boolean", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    replaces_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    object_deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_review_views",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_review_views", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_placements",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_placements", x => new { x.account_id, x.scope, x.position });
                    table.ForeignKey(
                        name: "fk_media_placements_media_items_media_id",
                        column: x => x.media_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_account_id_created_at",
                table: "media_items",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_sha256",
                table: "media_items",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_status_next_screen_at",
                table: "media_items",
                columns: new[] { "status", "next_screen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_media_placements_media_id",
                table: "media_placements",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_review_views_media_id_at",
                table: "media_review_views",
                columns: new[] { "media_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_placements");

            migrationBuilder.DropTable(
                name: "media_review_views");

            migrationBuilder.DropTable(
                name: "media_items");
        }
    }
}
