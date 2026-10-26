using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TravellerSearchArrival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arrival_checked_at",
                table: "trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arrival_market_id",
                table: "trips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arrival_method",
                table: "trips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arrival_outcome",
                table: "trips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arrived_at",
                table: "trips",
                type: "timestamp with time zone",
                nullable: true);

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
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    home_market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                name: "ix_trips_arrival_market_id",
                table: "trips",
                column: "arrival_market_id");

            migrationBuilder.CreateIndex(
                name: "ix_home_location_checks_account_id_checked_at",
                table: "home_location_checks",
                columns: new[] { "account_id", "checked_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_trips_markets_arrival_market_id",
                table: "trips",
                column: "arrival_market_id",
                principalTable: "markets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_trips_markets_arrival_market_id",
                table: "trips");

            migrationBuilder.DropTable(
                name: "home_location_checks");

            migrationBuilder.DropIndex(
                name: "ix_trips_arrival_market_id",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "arrival_checked_at",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "arrival_market_id",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "arrival_method",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "arrival_outcome",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "arrived_at",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "home_review_flagged_at",
                table: "member_profiles");
        }
    }
}
