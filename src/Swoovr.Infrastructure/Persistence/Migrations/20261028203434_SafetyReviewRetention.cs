using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SafetyReviewRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "evidence_purged_at",
                table: "moderation_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "no_review_retention_confirmed_at",
                table: "legal_holds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "no_review_retention_confirmed_by",
                table: "legal_holds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "no_review_retention_reason",
                table: "legal_holds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "related_account_ids",
                table: "legal_holds",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "retention_case_ids",
                table: "legal_holds",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "media_ids",
                table: "case_evidence",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deletion_finalizing_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_related_account_ids",
                table: "legal_holds",
                column: "related_account_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_retention_case_ids",
                table: "legal_holds",
                column: "retention_case_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_case_evidence_media_ids",
                table: "case_evidence",
                column: "media_ids")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_legal_holds_related_account_ids",
                table: "legal_holds");

            migrationBuilder.DropIndex(
                name: "ix_legal_holds_retention_case_ids",
                table: "legal_holds");

            migrationBuilder.DropIndex(
                name: "ix_case_evidence_media_ids",
                table: "case_evidence");

            migrationBuilder.DropColumn(
                name: "evidence_purged_at",
                table: "moderation_cases");

            migrationBuilder.DropColumn(
                name: "no_review_retention_confirmed_at",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "no_review_retention_confirmed_by",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "no_review_retention_reason",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "related_account_ids",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "retention_case_ids",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "media_ids",
                table: "case_evidence");

            migrationBuilder.DropColumn(
                name: "deletion_finalizing_at",
                table: "accounts");
        }
    }
}
