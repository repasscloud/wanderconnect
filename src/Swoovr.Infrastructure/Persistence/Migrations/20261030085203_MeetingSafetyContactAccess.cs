using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeetingSafetyContactAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting_safety_contact_challenge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_contact_challenge", x => x.id);
                    table.CheckConstraint("ck_meeting_contact_challenge", "octet_length(destination_hash)=32 AND octet_length(code_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '5 minutes' AND failed_attempts BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "fk_meeting_safety_contact_challenge_meeting_safety_recipients_re~",
                        column: x => x.recipient_id,
                        principalTable: "meeting_safety_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_contact_read_grant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_contact_read_grant", x => x.id);
                    table.CheckConstraint("ck_meeting_contact_read_grant", "octet_length(token_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '4 hours'");
                    table.ForeignKey(
                        name: "fk_meeting_safety_contact_read_grant_meeting_safety_recipients_r~",
                        column: x => x.recipient_id,
                        principalTable: "meeting_safety_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_contact_challenge_destination_hash_created_at",
                table: "meeting_safety_contact_challenge",
                columns: new[] { "destination_hash", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_contact_challenge_recipient_id_created_at",
                table: "meeting_safety_contact_challenge",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_contact_read_grant_recipient_id",
                table: "meeting_safety_contact_read_grant",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_contact_read_grant_token_hash",
                table: "meeting_safety_contact_read_grant",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_safety_contact_challenge");

            migrationBuilder.DropTable(
                name: "meeting_safety_contact_read_grant");
        }
    }
}
