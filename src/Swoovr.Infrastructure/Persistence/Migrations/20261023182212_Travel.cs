using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Travel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "home_geo_name_id",
                table: "member_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "geo_cities",
                columns: table => new
                {
                    geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ascii_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    admin1_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    admin1_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    population = table.Column<long>(type: "bigint", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geo_cities", x => x.geo_name_id);
                });

            migrationBuilder.CreateTable(
                name: "itineraries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itineraries", x => x.id);
                    table.ForeignKey(
                        name: "fk_itineraries_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    centre_geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    radius_km = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_markets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "geo_city_names",
                columns: table => new
                {
                    geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geo_city_names", x => new { x.key, x.geo_name_id });
                    table.ForeignKey(
                        name: "fk_geo_city_names_geo_cities_geo_name_id",
                        column: x => x.geo_name_id,
                        principalTable: "geo_cities",
                        principalColumn: "geo_name_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geo_name_id = table.Column<int>(type: "integer", nullable: false),
                    arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    departure = table.Column<DateOnly>(type: "date", nullable: false),
                    visible = table.Column<bool>(type: "boolean", nullable: false),
                    itinerary_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trips", x => x.id);
                    table.CheckConstraint("ck_trips_dates", "departure >= arrival");
                    table.ForeignKey(
                        name: "fk_trips_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_trips_geo_cities_geo_name_id",
                        column: x => x.geo_name_id,
                        principalTable: "geo_cities",
                        principalColumn: "geo_name_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trips_itineraries_itinerary_id",
                        column: x => x.itinerary_id,
                        principalTable: "itineraries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "markets",
                columns: new[] { "id", "active", "centre_geo_name_id", "name", "radius_km" },
                values: new object[] { "adelaide", true, 2078025, "Adelaide", 50.0 });

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_home_geo_name_id",
                table: "member_profiles",
                column: "home_geo_name_id");

            migrationBuilder.CreateIndex(
                name: "ix_connection_requests_trip_id",
                table: "connection_requests",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_city_names_geo_name_id",
                table: "geo_city_names",
                column: "geo_name_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_city_names_key_prefix",
                table: "geo_city_names",
                column: "key")
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_itineraries_account_id",
                table: "itineraries",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_trips_account_id_arrival",
                table: "trips",
                columns: new[] { "account_id", "arrival" });

            migrationBuilder.CreateIndex(
                name: "ix_trips_geo_name_id_departure",
                table: "trips",
                columns: new[] { "geo_name_id", "departure" });

            migrationBuilder.CreateIndex(
                name: "ix_trips_itinerary_id",
                table: "trips",
                column: "itinerary_id");

            migrationBuilder.AddForeignKey(
                name: "fk_connection_requests_trips_trip_id",
                table: "connection_requests",
                column: "trip_id",
                principalTable: "trips",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_member_profiles_geo_cities_home_geo_name_id",
                table: "member_profiles",
                column: "home_geo_name_id",
                principalTable: "geo_cities",
                principalColumn: "geo_name_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_connection_requests_trips_trip_id",
                table: "connection_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_member_profiles_geo_cities_home_geo_name_id",
                table: "member_profiles");

            migrationBuilder.DropTable(
                name: "geo_city_names");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "trips");

            migrationBuilder.DropTable(
                name: "geo_cities");

            migrationBuilder.DropTable(
                name: "itineraries");

            migrationBuilder.DropIndex(
                name: "ix_member_profiles_home_geo_name_id",
                table: "member_profiles");

            migrationBuilder.DropIndex(
                name: "ix_connection_requests_trip_id",
                table: "connection_requests");

            migrationBuilder.DropColumn(
                name: "home_geo_name_id",
                table: "member_profiles");
        }
    }
}
