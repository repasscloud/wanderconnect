using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HomeCityCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_location_checks");

            migrationBuilder.DropColumn(
                name: "home_review_flagged_at",
                table: "member_profiles");

            migrationBuilder.AddColumn<DateOnly>(
                name: "home_city_check_from",
                table: "member_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "home_city_dont_suggest_before",
                table: "member_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "home_city_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    located_days = table.Column<int>(type: "integer", nullable: false),
                    away_days = table.Column<int>(type: "integer", nullable: false),
                    excluded_days = table.Column<int>(type: "integer", nullable: false),
                    parts_covered = table.Column<int>(type: "integer", nullable: false),
                    away_parts = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    suppression = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ticket_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ticket_id = table.Column<long>(type: "bigint", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_home_city_checks", x => x.id);
                    table.ForeignKey(
                        name: "fk_home_city_checks_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "home_city_days",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_home_city_days", x => new { x.account_id, x.day });
                    table.ForeignKey(
                        name: "fk_home_city_days_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_home_city_checks_account_id_period_start",
                table: "home_city_checks",
                columns: new[] { "account_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_home_city_checks_checked_at",
                table: "home_city_checks",
                column: "checked_at");

            migrationBuilder.CreateIndex(
                name: "ix_home_city_checks_ticket_state_next_attempt_at",
                table: "home_city_checks",
                columns: new[] { "ticket_state", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_home_city_days_day",
                table: "home_city_days",
                column: "day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_city_checks");

            migrationBuilder.DropTable(
                name: "home_city_days");

            migrationBuilder.DropColumn(
                name: "home_city_check_from",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "home_city_dont_suggest_before",
                table: "member_profiles");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "home_review_flagged_at",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "home_location_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    home_market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_home_location_checks", x => x.id);
                    table.ForeignKey(
                        name: "fk_home_location_checks_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_home_location_checks_account_id_checked_at",
                table: "home_location_checks",
                columns: new[] { "account_id", "checked_at" });
        }
    }
}
