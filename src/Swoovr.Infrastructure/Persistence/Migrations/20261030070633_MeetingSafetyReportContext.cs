using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeetingSafetyReportContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "meeting_safety_session_id",
                table: "reports",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "meeting_safety_session_id",
                table: "reports");
        }
    }
}
