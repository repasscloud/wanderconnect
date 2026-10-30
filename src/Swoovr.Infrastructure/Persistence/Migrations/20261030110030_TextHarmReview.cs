using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TextHarmReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "text_revision",
                table: "member_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "text_revision",
                table: "member_modes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "text_harm_intent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    revision = table.Column<Guid>(type: "uuid", nullable: false),
                    sealed_text = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_text_harm_intent", x => x.id);
                    table.CheckConstraint("ck_text_harm_intent", "(processed_at IS NULL AND sealed_text IS NOT NULL AND octet_length(sealed_text)>=29 AND result IS NULL) OR (processed_at IS NOT NULL AND sealed_text IS NULL AND result IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "text_harm_review",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    detector_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    rules = table.Column<string[]>(type: "text[]", nullable: false),
                    categories = table.Column<string[]>(type: "text[]", nullable: false),
                    sealed_evidence = table.Column<byte[]>(type: "bytea", nullable: true),
                    evidence_disposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_text_harm_review", x => x.id);
                    table.CheckConstraint("ck_text_harm_review", "(sealed_evidence IS NOT NULL AND octet_length(sealed_evidence)>=29 AND evidence_disposed_at IS NULL) OR (sealed_evidence IS NULL AND evidence_disposed_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_text_harm_review_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_intent_account_id",
                table: "text_harm_intent",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_intent_account_id_source_source_id_revision",
                table: "text_harm_intent",
                columns: new[] { "account_id", "source", "source_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_intent_processed_at_created_at",
                table: "text_harm_intent",
                columns: new[] { "processed_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_review_case_id",
                table: "text_harm_review",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_review_intent_id",
                table: "text_harm_review",
                column: "intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_text_harm_review_subject_account_id",
                table: "text_harm_review",
                column: "subject_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "text_harm_intent");

            migrationBuilder.DropTable(
                name: "text_harm_review");

            migrationBuilder.DropColumn(
                name: "text_revision",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "text_revision",
                table: "member_modes");
        }
    }
}
