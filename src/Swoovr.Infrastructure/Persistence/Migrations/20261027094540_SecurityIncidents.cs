using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    subject_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_by = table.Column<Guid>(type: "uuid", nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assessment_due = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notifiable = table.Column<bool>(type: "boolean", nullable: true),
                    assessment_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    assessed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contained_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_incidents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_kind_subject_staff_id_detected_at",
                table: "security_incidents",
                columns: new[] { "kind", "subject_staff_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_status_severity",
                table: "security_incidents",
                columns: new[] { "status", "severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_incidents");
        }
    }
}
