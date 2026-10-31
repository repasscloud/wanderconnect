using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndividualContentRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "moderation_removal_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "moderation_removed_at",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_removal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sealed_snapshot = table.Column<byte[]>(type: "bytea", nullable: true),
                    snapshot_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    evidence_disposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_removal", x => x.id);
                    table.CheckConstraint("ck_content_removal_evidence", "(sealed_snapshot IS NOT NULL AND octet_length(snapshot_hash)=32 AND octet_length(sealed_snapshot)>=29 AND evidence_disposed_at IS NULL) OR (sealed_snapshot IS NULL AND octet_length(snapshot_hash)=0 AND evidence_disposed_at IS NOT NULL AND evidence_disposed_at>=removed_at)");
                    table.ForeignKey(
                        name: "fk_content_removal_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_removal_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_removal_case_id",
                table: "content_removal",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_removal_message_id",
                table: "content_removal",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_removal_report_id",
                table: "content_removal",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_removal_subject_account_id",
                table: "content_removal",
                column: "subject_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_removal");

            migrationBuilder.DropColumn(
                name: "moderation_removal_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "moderation_removed_at",
                table: "messages");
        }
    }
}
