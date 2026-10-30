using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BetaJourneyObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beta_journey_days",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    dataset = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    explore_succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    explore_has_peer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beta_journey_days", x => new { x.account_id, x.day, x.dataset });
                    table.CheckConstraint("ck_beta_journey_day", "dataset IN ('unknown','local-synthetic','production') AND (NOT explore_has_peer OR explore_succeeded)");
                    table.ForeignKey(
                        name: "fk_beta_journey_days_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_beta_journey_days_day",
                table: "beta_journey_days",
                column: "day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beta_journey_days");
        }
    }
}
