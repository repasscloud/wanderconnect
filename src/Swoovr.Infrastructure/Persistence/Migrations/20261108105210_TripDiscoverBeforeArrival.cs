using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D352 (#280): "Discover people there before arrival", independent of the trip's visibility. On by default: existing
    /// trips and hosts still on older code get true from the database default (the model always writes the value).
    /// </summary>
    public partial class TripDiscoverBeforeArrival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "discover_before_arrival",
                table: "trips",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "discover_before_arrival",
                table: "trips");
        }
    }
}
