using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentScanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment_scan_verdict",
                columns: table => new
                {
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scanner = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    engine_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    signature_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    scanned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment_scan_verdict", x => x.sha256);
                });

            migrationBuilder.CreateTable(
                name: "support_attachment_hold",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_article_id = table.Column<long>(type: "bigint", nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    author_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @private = table.Column<bool>(name: "private", type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scanner = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    engine_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    signature_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scan_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scan_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    release_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    release_article_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_attachment_hold", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_support_attachment_hold_group_id",
                table: "support_attachment_hold",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_support_attachment_hold_provider_ticket_id_provider_article~",
                table: "support_attachment_hold",
                columns: new[] { "provider_ticket_id", "provider_article_id" });

            migrationBuilder.CreateIndex(
                name: "ix_support_attachment_hold_state_next_attempt_at",
                table: "support_attachment_hold",
                columns: new[] { "state", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_scan_verdict");

            migrationBuilder.DropTable(
                name: "support_attachment_hold");
        }
    }
}
