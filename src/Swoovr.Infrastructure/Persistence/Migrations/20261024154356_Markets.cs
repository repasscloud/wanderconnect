using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Markets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "home_market_id",
                table: "member_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "home_market_moved_at",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "home_market_moved_to",
                table: "member_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "markets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "bot_markets",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authorised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    authorised_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bot_markets", x => new { x.account_id, x.market_id });
                    table.ForeignKey(
                        name: "fk_bot_markets_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "market_areas",
                columns: table => new
                {
                    market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    centre_geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    radius_km = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_areas", x => new { x.market_id, x.centre_geo_name_id });
                    table.CheckConstraint("ck_market_areas_radius", "radius_km > 0 AND radius_km <= 1000");
                    table.ForeignKey(
                        name: "fk_market_areas_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "market_interests",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_interests", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_market_interests_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_market_interests_geo_cities_geo_name_id",
                        column: x => x.geo_name_id,
                        principalTable: "geo_cities",
                        principalColumn: "geo_name_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_modes",
                columns: table => new
                {
                    market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_modes", x => new { x.market_id, x.mode_id });
                    table.ForeignKey(
                        name: "fk_market_modes_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_market_modes_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "market_areas",
                columns: new[] { "centre_geo_name_id", "market_id", "radius_km" },
                values: new object[] { 2078025, "adelaide", 50.0 });

            migrationBuilder.InsertData(
                table: "market_modes",
                columns: new[] { "market_id", "mode_id" },
                values: new object[,]
                {
                    { "adelaide", "activities" },
                    { "adelaide", "business" },
                    { "adelaide", "dating" },
                    { "adelaide", "friends" }
                });

            migrationBuilder.UpdateData(
                table: "markets",
                keyColumn: "id",
                keyValue: "adelaide",
                column: "status",
                value: "Beta");

            // D146: carry any other market's status and its centre and radius over as one area before the old columns go
            // (only Adelaide is seeded, which the rows above cover); existing bots keep appearing everywhere they did.
            migrationBuilder.Sql("UPDATE markets SET status = CASE WHEN active THEN 'Beta' ELSE 'Planned' END WHERE id <> 'adelaide';");
            migrationBuilder.Sql("INSERT INTO market_areas (market_id, centre_geo_name_id, radius_km) SELECT id, centre_geo_name_id, radius_km FROM markets WHERE id <> 'adelaide';");
            migrationBuilder.Sql("INSERT INTO bot_markets (account_id, market_id, authorised_at) SELECT account_id, '*', now() FROM member_profiles WHERE bot;");
            migrationBuilder.DropColumn(name: "active", table: "markets");
            migrationBuilder.DropColumn(name: "centre_geo_name_id", table: "markets");
            migrationBuilder.DropColumn(name: "radius_km", table: "markets");

            // Each existing home city gets its market: of the markets with an area that reaches it, an open one first,
            // then the nearest centre (PlaceNames.MarketOf). The API keeps it up to date from now on.
            migrationBuilder.Sql("""
                UPDATE member_profiles p SET home_market_id = (
                    SELECT a.market_id FROM market_areas a
                    JOIN markets m ON m.id = a.market_id
                    JOIN geo_cities c ON c.geo_name_id = a.centre_geo_name_id
                    JOIN geo_cities h ON h.geo_name_id = p.home_geo_name_id
                    CROSS JOIN LATERAL (SELECT 6371.0088 * 2 * asin(least(1, sqrt(
                        power(sin(radians(h.latitude - c.latitude) / 2), 2)
                        + cos(radians(c.latitude)) * cos(radians(h.latitude)) * power(sin(radians(h.longitude - c.longitude) / 2), 2)))) AS km) d
                    WHERE d.km <= a.radius_km
                    ORDER BY (m.status IN ('Beta', 'Active')) DESC, d.km, m.id
                    LIMIT 1)
                WHERE p.home_geo_name_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_home_market_id",
                table: "member_profiles",
                column: "home_market_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_interests_geo_name_id",
                table: "market_interests",
                column: "geo_name_id");

            migrationBuilder.CreateIndex(
                name: "ix_market_modes_mode_id",
                table: "market_modes",
                column: "mode_id");

            migrationBuilder.AddForeignKey(
                name: "fk_member_profiles_markets_home_market_id",
                table: "member_profiles",
                column: "home_market_id",
                principalTable: "markets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_member_profiles_markets_home_market_id",
                table: "member_profiles");

            migrationBuilder.DropTable(
                name: "bot_markets");

            migrationBuilder.DropTable(
                name: "market_areas");

            migrationBuilder.DropTable(
                name: "market_interests");

            migrationBuilder.DropTable(
                name: "market_modes");

            migrationBuilder.DropIndex(
                name: "ix_member_profiles_home_market_id",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "home_market_id",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "home_market_moved_at",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "home_market_moved_to",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "status",
                table: "markets");

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "markets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "centre_geo_name_id",
                table: "markets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "radius_km",
                table: "markets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.UpdateData(
                table: "markets",
                keyColumn: "id",
                keyValue: "adelaide",
                columns: new[] { "active", "centre_geo_name_id", "radius_km" },
                values: new object[] { true, 2078025, 50.0 });
        }
    }
}
