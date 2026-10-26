using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Moderation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entitlements", x => x.id);
                    table.CheckConstraint("ck_entitlements_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_entitlements_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    queue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_moderation_cases_accounts_subject_account_id",
                        column: x => x.subject_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_categories",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_member_links",
                columns: table => new
                {
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_member_links", x => x.staff_account_id);
                    table.ForeignKey(
                        name: "fk_staff_member_links_accounts_member_account_id",
                        column: x => x.member_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_staff_member_links_accounts_staff_account_id",
                        column: x => x.staff_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sanctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hide_from_discovery = table.Column<bool>(type: "boolean", nullable: false),
                    no_new_requests = table.Column<bool>(type: "boolean", nullable: false),
                    no_messages = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lifted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lifted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sanctions", x => x.id);
                    table.CheckConstraint("ck_sanctions_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_sanctions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sanctions_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.CheckConstraint("ck_reports_not_self", "reporter_account_id <> subject_account_id");
                    table.ForeignKey(
                        name: "fk_reports_accounts_reporter_account_id",
                        column: x => x.reporter_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_accounts_subject_account_id",
                        column: x => x.subject_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reports_report_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appeals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sanction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appeals", x => x.id);
                    table.ForeignKey(
                        name: "fk_appeals_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appeals_sanctions_sanction_id",
                        column: x => x.sanction_id,
                        principalTable: "sanctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sanction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_notices", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_notices_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_notices_sanctions_sanction_id",
                        column: x => x.sanction_id,
                        principalTable: "sanctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_evidence_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_case_evidence_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "report_categories",
                columns: new[] { "id", "available", "label", "priority", "sort_order" },
                values: new object[,]
                {
                    { "fake", true, "Fake profile or impersonation", "Normal", 7 },
                    { "harassment", true, "Harassment or bullying", "High", 3 },
                    { "hate", true, "Hate or discrimination", "High", 4 },
                    { "inappropriate", true, "Inappropriate profile content", "Normal", 9 },
                    { "other", true, "Something else", "Normal", 10 },
                    { "scam", true, "Scam, fraud or selling", "High", 6 },
                    { "sexual", true, "Unwanted sexual content or behaviour", "High", 5 },
                    { "spam", true, "Spam", "Normal", 8 },
                    { "threats", true, "Threats or violence", "Urgent", 1 },
                    { "underage", true, "Might be under 18", "Urgent", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_appeals_account_id",
                table: "appeals",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_appeals_sanction_id",
                table: "appeals",
                column: "sanction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_evidence_case_id",
                table: "case_evidence",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_evidence_report_id",
                table: "case_evidence",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_entitlements_account_id_revoked_at",
                table: "entitlements",
                columns: new[] { "account_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_member_notices_account_id_created_at",
                table: "member_notices",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_member_notices_sanction_id",
                table: "member_notices",
                column: "sanction_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_one_open",
                table: "moderation_cases",
                column: "subject_account_id",
                unique: true,
                filter: "status IN ('Open', 'UnderReview')");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_queue_status_priority",
                table: "moderation_cases",
                columns: new[] { "queue", "status", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_case_id",
                table: "reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_category_id",
                table: "reports",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_account_id_created_at",
                table: "reports",
                columns: new[] { "reporter_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_subject_account_id",
                table: "reports",
                column: "subject_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_sanctions_account_id_lifted_at",
                table: "sanctions",
                columns: new[] { "account_id", "lifted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sanctions_case_id",
                table: "sanctions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_member_links_member_account_id",
                table: "staff_member_links",
                column: "member_account_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appeals");

            migrationBuilder.DropTable(
                name: "case_evidence");

            migrationBuilder.DropTable(
                name: "entitlements");

            migrationBuilder.DropTable(
                name: "member_notices");

            migrationBuilder.DropTable(
                name: "staff_member_links");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "sanctions");

            migrationBuilder.DropTable(
                name: "report_categories");

            migrationBuilder.DropTable(
                name: "moderation_cases");
        }
    }
}
