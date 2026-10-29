using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailAcceptanceClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_accepted_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            // Legacy rows do not prove the first email-acceptance instant. Pin a migration-time/skew watermark;
            // the same email stays usable, but an older differing claim must reauthenticate before adoption.
            migrationBuilder.Sql("UPDATE accounts SET email_accepted_at = CURRENT_TIMESTAMP + interval '5 seconds' WHERE email IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_accepted_at",
                table: "accounts");
        }
    }
}
