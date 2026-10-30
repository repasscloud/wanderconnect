using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModerationTransparencyReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "moderation_inspection_totals",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    environment = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    definition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    completed_inspections = table.Column<long>(type: "bigint", nullable: false),
                    incomplete_history = table.Column<long>(type: "bigint", nullable: false),
                    created_candidates = table.Column<long>(type: "bigint", nullable: false),
                    failed_processing_attempts = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_inspection_totals", x => new { x.day, x.environment, x.definition });
                    table.CheckConstraint("ck_inspection_total_counts", "completed_inspections>=0 AND incomplete_history>=0 AND incomplete_history<=completed_inspections AND created_candidates>=0 AND created_candidates<=completed_inspections AND failed_processing_attempts>=0 AND (definition<>'image-screening-unversioned' OR incomplete_history=0)");
                    table.CheckConstraint("ck_inspection_total_dimensions", "environment IN ('unknown','production','local-synthetic') AND definition IN ('local-text-rules-v1','image-screening-unversioned')");
                });

            migrationBuilder.CreateTable(
                name: "moderation_transparency_facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    home_country_at_action = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    country_provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reviewed_policy_basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    basis_reviewed = table.Column<bool>(type: "boolean", nullable: false),
                    detection_mechanism = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    enforcement_mechanism = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    automation_definition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    initiative = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    environment = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_transparency_facts", x => x.id);
                    table.CheckConstraint("ck_transparency_fact_basis", "reviewed_policy_basis IN ('unknown','harassment','threats-violence','sexual-abuse','child-safety','fraud-scam','hate-discrimination','privacy','other-reviewed') AND basis_reviewed=(reviewed_policy_basis<>'unknown')");
                    table.CheckConstraint("ck_transparency_fact_country", "(country_provenance='member-declared-home-at-action' AND home_country_at_action IN ('AU','other')) OR (country_provenance='unknown' AND home_country_at_action='unknown')");
                    table.CheckConstraint("ck_transparency_fact_mechanisms", "detection_mechanism IN ('unknown','member-report','staff-review','local-text-rules','image-screening') AND enforcement_mechanism IN ('unknown','human','automated-screening') AND initiative IN ('unknown','reported','own-initiative') AND automation_definition IN ('none','unknown','local-text-rules-v1','image-screening-unversioned') AND environment IN ('unknown','production','local-synthetic')");
                    table.CheckConstraint("ck_transparency_fact_source", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND subject_account_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (source_case_id IS NULL OR source_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((source_kind='sanction' AND action='termination') OR (source_kind IN ('content-removal','media-decision') AND action='content-removal')) AND recorded_at >= action_at");
                });

            migrationBuilder.CreateTable(
                name: "moderation_transparency_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ending_year = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    definition_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    aggregate_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_transparency_snapshots", x => x.id);
                    table.CheckConstraint("ck_transparency_snapshot", "ending_year BETWEEN 2 AND 9998 AND revision>0 AND fingerprint ~ '^[0-9a-f]{64}$' AND octet_length(aggregate_json)<=200000 AND jsonb_typeof(aggregate_json::jsonb)='object'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_transparency_facts_action_at_environment",
                table: "moderation_transparency_facts",
                columns: new[] { "action_at", "environment" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_transparency_facts_source_case_id_subject_accoun~",
                table: "moderation_transparency_facts",
                columns: new[] { "source_case_id", "subject_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_transparency_facts_source_kind_source_id",
                table: "moderation_transparency_facts",
                columns: new[] { "source_kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_transparency_snapshots_ending_year_fingerprint",
                table: "moderation_transparency_snapshots",
                columns: new[] { "ending_year", "fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_transparency_snapshots_ending_year_revision",
                table: "moderation_transparency_snapshots",
                columns: new[] { "ending_year", "revision" },
                unique: true);
            migrationBuilder.Sql(ModerationTransparencyGuardSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ModerationTransparencyGuardSql.Down);
            migrationBuilder.DropTable(
                name: "moderation_inspection_totals");

            migrationBuilder.DropTable(
                name: "moderation_transparency_facts");

            migrationBuilder.DropTable(
                name: "moderation_transparency_snapshots");
        }
    }
}
