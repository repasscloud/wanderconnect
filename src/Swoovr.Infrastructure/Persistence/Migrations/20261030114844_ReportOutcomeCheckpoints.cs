using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportOutcomeCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_reports_id_case_id_reporter_account_id",
                table: "reports",
                columns: new[] { "id", "case_id", "reporter_account_id" });

            migrationBuilder.CreateTable(
                name: "report_outcome_states",
                columns: table => new
                {
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_outcome_states", x => x.report_id);
                    table.CheckConstraint("ck_report_outcome_checkpoint", "revision>=0 AND (fingerprint IS NULL OR fingerprint ~ '^[0-9a-f]{64}$')");
                    table.ForeignKey(
                        name: "fk_report_outcome_states_moderation_cases_source_case_id",
                        column: x => x.source_case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_report_outcome_states_reports_report_id_source_case_id_sour~",
                        columns: x => new { x.report_id, x.source_case_id, x.source_account_id },
                        principalTable: "reports",
                        principalColumns: new[] { "id", "case_id", "reporter_account_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_report_outcome_states_report_id_source_case_id_source_accou~",
                table: "report_outcome_states",
                columns: new[] { "report_id", "source_case_id", "source_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_report_outcome_states_source_case_id_source_account_id",
                table: "report_outcome_states",
                columns: new[] { "source_case_id", "source_account_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_outcome_states");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_reports_id_case_id_reporter_account_id",
                table: "reports");
        }
    }
}
