using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Matching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_identities",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gender_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    gender_description = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    pronouns = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    orientation_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    show_gender = table.Column<bool>(type: "boolean", nullable: false),
                    show_orientation = table.Column<bool>(type: "boolean", nullable: false),
                    show_age = table.Column<bool>(type: "boolean", nullable: false),
                    dating_show_me = table.Column<List<string>>(type: "text[]", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_identities", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_member_identities_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mode_filters",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    min_age = table.Column<int>(type: "integer", nullable: true),
                    max_age = table.Column<int>(type: "integer", nullable: true),
                    required_chips = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    required_match = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    excluded_chips = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    id_verified_only = table.Column<bool>(type: "boolean", nullable: false),
                    visitors = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    max_distance_km = table.Column<int>(type: "integer", nullable: true),
                    sort = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mode_filters", x => new { x.account_id, x.mode_id });
                    table.CheckConstraint("ck_mode_filters_ages", "(min_age IS NULL OR min_age >= 18) AND (max_age IS NULL OR max_age <= 120) AND (min_age IS NULL OR max_age IS NULL OR min_age <= max_age)");
                    table.ForeignKey(
                        name: "fk_mode_filters_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mode_filters_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_options",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_options", x => new { x.kind, x.id });
                });

            migrationBuilder.InsertData(
                table: "profile_options",
                columns: new[] { "id", "kind", "available", "label", "sort_order" },
                values: new object[,]
                {
                    { "another", "gender", true, "Another gender", 4 },
                    { "man", "gender", true, "Man", 1 },
                    { "non-binary", "gender", true, "Non-binary", 3 },
                    { "unsaid", "gender", true, "Prefer not to say", 5 },
                    { "woman", "gender", true, "Woman", 2 },
                    { "another", "orientation", true, "Another orientation", 9 },
                    { "asexual", "orientation", true, "Asexual", 6 },
                    { "bisexual", "orientation", true, "Bisexual", 4 },
                    { "gay", "orientation", true, "Gay", 2 },
                    { "lesbian", "orientation", true, "Lesbian", 3 },
                    { "pansexual", "orientation", true, "Pansexual", 5 },
                    { "queer", "orientation", true, "Queer", 7 },
                    { "questioning", "orientation", true, "Questioning", 8 },
                    { "straight", "orientation", true, "Straight", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_mode_filters_mode_id",
                table: "mode_filters",
                column: "mode_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_identities");

            migrationBuilder.DropTable(
                name: "mode_filters");

            migrationBuilder.DropTable(
                name: "profile_options");
        }
    }
}
