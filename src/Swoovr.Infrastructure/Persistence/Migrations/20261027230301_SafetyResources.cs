using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SafetyResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "safety_countries",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    readiness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_reviewed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    review_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_safety_countries", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "safety_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_safety_revisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "safety_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    language_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phone_display = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    website_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    source_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_verified_on = table.Column<DateOnly>(type: "date", nullable: true),
                    review_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_safety_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_safety_resources_safety_countries_country_code",
                        column: x => x.country_code,
                        principalTable: "safety_countries",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "safety_countries",
                columns: new[] { "code", "enabled", "last_reviewed_on", "name", "notes", "owner", "readiness", "review_due_on", "updated_at", "version" },
                values: new object[,]
                {
                    { "AU", true, new DateOnly(2026, 10, 27), "Australia", "Owner-approved list for the Adelaide beta (Q77, D190).", "Trust & Safety", "Ready", new DateOnly(2027, 1, 25), new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { "GB", true, null, "United Kingdom", "Illustrative only; not approved for launch.", "Trust & Safety", "Demo", null, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { "NZ", true, null, "New Zealand", "Starter entries; research and verify every category before enabling an NZ market.", "Trust & Safety", "NotReady", null, new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 }
                });

            migrationBuilder.InsertData(
                table: "safety_resources",
                columns: new[] { "id", "category", "country_code", "description", "display_order", "enabled", "language_code", "last_verified_on", "name", "phone", "phone_display", "region_code", "review_due_on", "source_url", "updated_at", "updated_by", "website_url" },
                values: new object[,]
                {
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000001"), "Emergency", "AU", "Police, fire and ambulance. Call if you or someone else is in danger now.", 10, true, "en-AU", new DateOnly(2026, 10, 27), "Triple Zero (000)", "000", "000", null, new DateOnly(2027, 1, 25), "https://www.triplezero.gov.au/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.triplezero.gov.au/" },
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000002"), "PoliceNonEmergency", "AU", "Report a crime to police when it isn't an emergency.", 20, true, "en-AU", new DateOnly(2026, 10, 27), "Police Assistance Line", "131444", "131 444", null, new DateOnly(2027, 1, 25), "https://www.police.sa.gov.au/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.police.sa.gov.au/" },
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000003"), "SexualViolence", "AU", "24/7 support for sexual assault and domestic or family violence.", 30, true, "en-AU", new DateOnly(2026, 10, 27), "1800RESPECT", "1800737732", "1800 737 732", null, new DateOnly(2027, 1, 25), "https://www.1800respect.org.au/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.1800respect.org.au/" },
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000004"), "Crisis", "AU", "24/7 crisis support.", 40, true, "en-AU", new DateOnly(2026, 10, 27), "Lifeline", "131114", "13 11 14", null, new DateOnly(2027, 1, 25), "https://www.lifeline.org.au/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.lifeline.org.au/" },
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000005"), "OnlineSafety", "AU", "Report serious online abuse and image-based abuse.", 50, true, "en-AU", new DateOnly(2026, 10, 27), "eSafety Commissioner", null, null, null, new DateOnly(2027, 1, 25), "https://www.esafety.gov.au/report", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.esafety.gov.au/report" },
                    { new Guid("6f1a0c1e-0001-4000-8000-000000000006"), "ChildExploitation", "AU", "Report online child sexual exploitation to the Australian Centre to Counter Child Exploitation.", 60, true, "en-AU", new DateOnly(2026, 10, 27), "ACCCE", null, null, null, new DateOnly(2027, 1, 25), "https://www.accce.gov.au/report", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.accce.gov.au/report" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000001"), "Emergency", "NZ", "Police, fire or ambulance in an emergency.", 10, true, "en-NZ", null, "Emergency services (111)", "111", "111", null, null, "https://www.police.govt.nz/advice-services/how-report-crime-or-incident", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.police.govt.nz/" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000002"), "PoliceNonEmergency", "NZ", "Non-urgent police matters.", 20, true, "en-NZ", null, "New Zealand Police 105", "105", "105", null, null, "https://www.police.govt.nz/faq/why-has-police-introduced-non-emergency-number", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.police.govt.nz/105support" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000003"), "OnlineSafety", "NZ", "Help with online safety.", 50, true, "en-NZ", null, "Netsafe", null, null, null, null, "https://netsafe.org.nz/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://netsafe.org.nz/" },
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000001"), "Emergency", "GB", "Police, fire or ambulance in an emergency.", 10, true, "en-GB", null, "Emergency services (999)", "999", "999", null, null, "https://www.gov.uk/contact-police", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.gov.uk/contact-police" },
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000002"), "PoliceNonEmergency", "GB", "Non-emergency police contact.", 20, true, "en-GB", null, "Police 101", "101", "101", null, null, "https://www.police.uk/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.police.uk/" },
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000003"), "ChildExploitation", "GB", "Report online child sexual abuse concerns.", 60, true, "en-GB", null, "CEOP Safety Centre", null, null, null, null, "https://www.ceop.police.uk/Safety-Centre/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.ceop.police.uk/Safety-Centre/" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_safety_resources_country_code_enabled_display_order",
                table: "safety_resources",
                columns: new[] { "country_code", "enabled", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_safety_revisions_country_code_at",
                table: "safety_revisions",
                columns: new[] { "country_code", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "safety_resources");

            migrationBuilder.DropTable(
                name: "safety_revisions");

            migrationBuilder.DropTable(
                name: "safety_countries");
        }
    }
}
