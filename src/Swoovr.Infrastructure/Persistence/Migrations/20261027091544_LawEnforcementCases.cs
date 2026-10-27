using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LawEnforcementCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "legal_holds",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lawful_basis",
                table: "legal_holds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "le_case_id",
                table: "legal_holds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requesting_body",
                table: "legal_holds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correction_reason",
                table: "evidence_exports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "le_case_id",
                table: "evidence_exports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "package_sha256",
                table: "evidence_exports",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplements_export_id",
                table: "evidence_exports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evidence_redactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    export_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fields = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    redacted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_redactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_redactions_evidence_exports_export_id",
                        column: x => x.export_id,
                        principalTable: "evidence_exports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "le_cases",
                schema: "restricted",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    agency_country = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agency_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    officer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    officer_rank = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    officer_service_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    officer_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    officer_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verification_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    verification_evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    legal_assessment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    legal_instrument = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    mandatory = table.Column<bool>(type: "boolean", nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    app65_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    app65_by = table.Column<Guid>(type: "uuid", nullable: true),
                    app65_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    privileged = table.Column<bool>(type: "boolean", nullable: false),
                    confidential = table.Column<bool>(type: "boolean", nullable: false),
                    confidentiality_assessment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    statutory_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_response_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_update_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_le_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "le_case_people",
                schema: "restricted",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_le_case_people", x => new { x.case_id, x.account_id });
                    table.ForeignKey(
                        name: "fk_le_case_people_le_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "restricted",
                        principalTable: "le_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "le_notifications",
                schema: "restricted",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reconsider_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_le_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_le_notifications_le_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "restricted",
                        principalTable: "le_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "le_requests",
                schema: "restricted",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    break_glass = table.Column<bool>(type: "boolean", nullable: false),
                    retrospective_due = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retrospective_by = table.Column<Guid>(type: "uuid", nullable: true),
                    retrospective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retrospective_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_le_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_le_requests_le_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "restricted",
                        principalTable: "le_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "le_transfers",
                schema: "restricted",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    export_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    receipt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    receipt_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_le_transfers", x => x.id);
                    table.ForeignKey(
                        name: "fk_le_transfers_le_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "restricted",
                        principalTable: "le_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_le_case_id",
                table: "legal_holds",
                column: "le_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_exports_le_case_id",
                table: "evidence_exports",
                column: "le_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_redactions_export_id",
                table: "evidence_redactions",
                column: "export_id");

            migrationBuilder.CreateIndex(
                name: "ix_le_case_people_account_id",
                schema: "restricted",
                table: "le_case_people",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_le_cases_reference",
                schema: "restricted",
                table: "le_cases",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_le_cases_retain_until",
                schema: "restricted",
                table: "le_cases",
                column: "retain_until");

            migrationBuilder.CreateIndex(
                name: "ix_le_cases_status_received_at",
                schema: "restricted",
                table: "le_cases",
                columns: new[] { "status", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_le_notifications_case_id_account_id",
                schema: "restricted",
                table: "le_notifications",
                columns: new[] { "case_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_le_notifications_reconsider_at",
                schema: "restricted",
                table: "le_notifications",
                column: "reconsider_at");

            migrationBuilder.CreateIndex(
                name: "ix_le_requests_case_id",
                schema: "restricted",
                table: "le_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_le_transfers_case_id",
                schema: "restricted",
                table: "le_transfers",
                column: "case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_redactions");

            migrationBuilder.DropTable(
                name: "le_case_people",
                schema: "restricted");

            migrationBuilder.DropTable(
                name: "le_notifications",
                schema: "restricted");

            migrationBuilder.DropTable(
                name: "le_requests",
                schema: "restricted");

            migrationBuilder.DropTable(
                name: "le_transfers",
                schema: "restricted");

            migrationBuilder.DropTable(
                name: "le_cases",
                schema: "restricted");

            migrationBuilder.DropIndex(
                name: "ix_legal_holds_le_case_id",
                table: "legal_holds");

            migrationBuilder.DropIndex(
                name: "ix_evidence_exports_le_case_id",
                table: "evidence_exports");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "lawful_basis",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "le_case_id",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "requesting_body",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "correction_reason",
                table: "evidence_exports");

            migrationBuilder.DropColumn(
                name: "le_case_id",
                table: "evidence_exports");

            migrationBuilder.DropColumn(
                name: "package_sha256",
                table: "evidence_exports");

            migrationBuilder.DropColumn(
                name: "supplements_export_id",
                table: "evidence_exports");
        }
    }
}
