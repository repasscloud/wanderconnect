using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageProcessingStaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "image_processing_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_etag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    lifecycle_revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    upload_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    results_json = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    variants_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    completed_event_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_image_processing_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "image_processing_receipts",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_image_processing_receipts", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_jobs_account_id_created_at",
                table: "image_processing_jobs",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_jobs_image_id",
                table: "image_processing_jobs",
                column: "image_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_jobs_source_key",
                table: "image_processing_jobs",
                column: "source_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_jobs_state_created_at",
                table: "image_processing_jobs",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_jobs_upload_id",
                table: "image_processing_jobs",
                column: "upload_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_image_processing_receipts_job_id",
                table: "image_processing_receipts",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "image_processing_jobs");

            migrationBuilder.DropTable(
                name: "image_processing_receipts");
        }
    }
}
