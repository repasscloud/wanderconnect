using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D348 (#278): one sealed Near me reading per member (a ~500 m cell, never a history), deleted with the account and
    /// purged five minutes after its two hours of use. A new table only.
    /// </summary>
    public partial class MemberLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_locations",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    sealed_cell = table.Column<byte[]>(type: "bytea", maxLength: 256, nullable: false),
                    accuracy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_locations", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_member_locations_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_locations_market_id_observed_at",
                table: "member_locations",
                columns: new[] { "market_id", "observed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_member_locations_observed_at",
                table: "member_locations",
                column: "observed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_locations");
        }
    }
}
