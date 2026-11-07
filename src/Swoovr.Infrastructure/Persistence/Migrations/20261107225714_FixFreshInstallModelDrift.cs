using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixFreshInstallModelDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0003-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0003-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0003-4000-8000-000000000003"));

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000001"),
                columns: new[] { "last_verified_on", "review_due_on" },
                values: new object[] { new DateOnly(2026, 10, 27), new DateOnly(2027, 1, 25) });

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000002"),
                columns: new[] { "description", "last_verified_on", "review_due_on", "source_url", "website_url" },
                values: new object[] { "Report crimes or incidents that do not require an immediate police response. Available 24/7.", new DateOnly(2026, 10, 27), new DateOnly(2027, 1, 25), "https://www.police.govt.nz/105support", "https://105.police.govt.nz/" });

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000003"),
                columns: new[] { "description", "last_verified_on", "phone", "phone_display", "review_due_on", "source_url", "website_url" },
                values: new object[] { "Report online abuse, harassment, bullying or image-based abuse. Phone support Monday to Friday, 9am to 5pm.", new DateOnly(2026, 10, 27), "0508638723", "0508 638 723", new DateOnly(2027, 1, 25), "https://netsafe.org.nz/netsafe/contact-us", "https://netsafe.org.nz/report" });

            migrationBuilder.InsertData(
                table: "safety_resources",
                columns: new[] { "id", "category", "country_code", "description", "display_order", "enabled", "language_code", "last_verified_on", "name", "phone", "phone_display", "region_code", "review_due_on", "source_url", "updated_at", "updated_by", "website_url" },
                values: new object[,]
                {
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000004"), "Crisis", "NZ", "Free, confidential emotional and mental health support, available 24/7 by phone or text.", 30, true, "en-NZ", new DateOnly(2026, 10, 27), "Need to Talk? 1737", "1737", "1737", null, new DateOnly(2027, 1, 25), "https://1737.org.nz/how-1737-works", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://1737.org.nz/" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000005"), "SexualViolence", "NZ", "Free, confidential 24/7 support for people affected by sexual harm, assault or abuse. Call or text 4334.", 40, true, "en-NZ", new DateOnly(2026, 10, 27), "Safe to Talk", "0800044334", "0800 044 334", null, new DateOnly(2027, 1, 25), "https://www.safetotalk.nz/contact-us/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.safetotalk.nz/" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000006"), "DomesticViolence", "NZ", "Free, confidential 24/7 support for anyone affected by domestic or family violence, including safety planning and referrals.", 45, true, "en-NZ", new DateOnly(2026, 10, 27), "Are You OK", "0800456450", "0800 456 450", null, new DateOnly(2027, 1, 25), "https://www.areyouok.org.nz/get-support/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.areyouok.org.nz/" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000007"), "ChildExploitation", "NZ", "Report suspected online child sexual abuse or exploitation material. For grooming or immediate danger, contact Police.", 60, true, "en-NZ", new DateOnly(2026, 10, 27), "Department of Internal Affairs - Digital Child Exploitation Team", null, null, null, new DateOnly(2027, 1, 25), "https://www.dia.govt.nz/Digital-Safety-Report-Online-Child-Exploitation-Material", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.dia.govt.nz/Digital-Safety-Report-Online-Child-Exploitation-Material" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000008"), "Other", "NZ", "Free, confidential 24/7 support for victims of crime, their families and witnesses of traumatic events.", 70, true, "en-NZ", new DateOnly(2026, 10, 27), "Victim Support", "0800842846", "0800 842 846", null, new DateOnly(2027, 1, 25), "https://www.victimsupport.org.nz/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.victimsupport.org.nz/" },
                    { new Guid("6f1a0c1e-0002-4000-8000-000000000009"), "DomesticViolence", "NZ", "Free 24/7 crisis support and assistance for women and children experiencing domestic or family violence.", 46, true, "en-NZ", new DateOnly(2026, 10, 27), "Women's Refuge", "0800733843", "0800 733 843", null, new DateOnly(2027, 1, 25), "https://womensrefuge.org.nz/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://womensrefuge.org.nz/" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000009"));

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000001"),
                columns: new[] { "last_verified_on", "review_due_on" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000002"),
                columns: new[] { "description", "last_verified_on", "review_due_on", "source_url", "website_url" },
                values: new object[] { "Non-urgent police matters.", null, null, "https://www.police.govt.nz/faq/why-has-police-introduced-non-emergency-number", "https://www.police.govt.nz/105support" });

            migrationBuilder.UpdateData(
                table: "safety_resources",
                keyColumn: "id",
                keyValue: new Guid("6f1a0c1e-0002-4000-8000-000000000003"),
                columns: new[] { "description", "last_verified_on", "phone", "phone_display", "review_due_on", "source_url", "website_url" },
                values: new object[] { "Help with online safety.", null, null, null, null, "https://netsafe.org.nz/", "https://netsafe.org.nz/" });

            migrationBuilder.InsertData(
                table: "safety_resources",
                columns: new[] { "id", "category", "country_code", "description", "display_order", "enabled", "language_code", "last_verified_on", "name", "phone", "phone_display", "region_code", "review_due_on", "source_url", "updated_at", "updated_by", "website_url" },
                values: new object[,]
                {
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000001"), "Emergency", "GB", "Police, fire or ambulance in an emergency.", 10, true, "en-GB", null, "Emergency services (999)", "999", "999", null, null, "https://www.gov.uk/contact-police", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.gov.uk/contact-police" },
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000002"), "PoliceNonEmergency", "GB", "Non-emergency police contact.", 20, true, "en-GB", null, "Police 101", "101", "101", null, null, "https://www.police.uk/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.police.uk/" },
                    { new Guid("6f1a0c1e-0003-4000-8000-000000000003"), "ChildExploitation", "GB", "Report online child sexual abuse concerns.", 60, true, "en-GB", null, "CEOP Safety Centre", null, null, null, null, "https://www.ceop.police.uk/Safety-Centre/", new DateTimeOffset(new DateTime(2026, 10, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "https://www.ceop.police.uk/Safety-Centre/" }
                });
        }
    }
}
