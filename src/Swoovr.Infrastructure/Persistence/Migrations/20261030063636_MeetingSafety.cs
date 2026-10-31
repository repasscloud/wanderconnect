using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeetingSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meeting_safety_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_in_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    final_check_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    venue_or_area = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    share_place = table.Column<bool>(type: "boolean", nullable: false),
                    member_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    after_prompt_dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    private_feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_sessions", x => x.id);
                    table.CheckConstraint("ck_meeting_safety_clocks", "final_check_in_at>starts_at AND ((closed_at IS NULL AND purge_after IS NULL) OR (closed_at IS NOT NULL AND purge_after=closed_at+interval '720 hours'))");
                });

            migrationBuilder.CreateTable(
                name: "trusted_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    preferred_first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    channel = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    destination_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    destination_lookup_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    consent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    consent_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    consent_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consent_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    management_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invitation_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trusted_contacts", x => x.id);
                    table.CheckConstraint("ck_trusted_contact_channel", "channel IN ('email','sms')");
                    table.CheckConstraint("ck_trusted_contact_hash", "octet_length(destination_lookup_hash)=32 AND octet_length(destination_encrypted)>=29 AND (consent_token_hash IS NULL OR octet_length(consent_token_hash)=32) AND (management_token_hash IS NULL OR octet_length(management_token_hash)=32)");
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_check_ins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    grace_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    response = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_check_ins", x => x.id);
                    table.CheckConstraint("ck_meeting_safety_grace", "grace_ends_at=due_at+interval '30 minutes' AND (escalated_at IS NULL OR escalated_at>=grace_ends_at)");
                    table.ForeignKey(
                        name: "fk_meeting_safety_check_ins_meeting_safety_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "meeting_safety_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    check_in_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_safety_events_meeting_safety_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "meeting_safety_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trusted_contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notification_status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_safety_recipients_meeting_safety_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "meeting_safety_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_safety_recipients_trusted_contacts_trusted_contact_id",
                        column: x => x.trusted_contact_id,
                        principalTable: "trusted_contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_safety_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_safety_links", x => x.id);
                    table.CheckConstraint("ck_meeting_safety_token", "octet_length(token_hash)=32 AND expires_at>created_at AND expires_at<=created_at+interval '720 hours'");
                    table.ForeignKey(
                        name: "fk_meeting_safety_links_meeting_safety_recipients_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "meeting_safety_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_check_ins_grace_ends_at_escalated_at",
                table: "meeting_safety_check_ins",
                columns: new[] { "grace_ends_at", "escalated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_check_ins_session_id_due_at",
                table: "meeting_safety_check_ins",
                columns: new[] { "session_id", "due_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_events_session_id_command_id",
                table: "meeting_safety_events",
                columns: new[] { "session_id", "command_id" },
                unique: true,
                filter: "command_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_events_session_id_id",
                table: "meeting_safety_events",
                columns: new[] { "session_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_links_expires_at",
                table: "meeting_safety_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_links_recipient_id",
                table: "meeting_safety_links",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_links_token_hash",
                table: "meeting_safety_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_outbox_dedupe_key",
                table: "meeting_safety_outbox",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_outbox_member_id",
                table: "meeting_safety_outbox",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_outbox_status_next_attempt_at",
                table: "meeting_safety_outbox",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_recipients_session_id_trusted_contact_id",
                table: "meeting_safety_recipients",
                columns: new[] { "session_id", "trusted_contact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_recipients_trusted_contact_id",
                table: "meeting_safety_recipients",
                column: "trusted_contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_sessions_closed_at_final_check_in_at",
                table: "meeting_safety_sessions",
                columns: new[] { "closed_at", "final_check_in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_sessions_member_id_created_at",
                table: "meeting_safety_sessions",
                columns: new[] { "member_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_safety_sessions_purge_after",
                table: "meeting_safety_sessions",
                column: "purge_after");

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_consent_token_hash",
                table: "trusted_contacts",
                column: "consent_token_hash",
                unique: true,
                filter: "consent_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_destination_lookup_hash",
                table: "trusted_contacts",
                column: "destination_lookup_hash");

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_management_token_hash",
                table: "trusted_contacts",
                column: "management_token_hash",
                unique: true,
                filter: "management_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_member_id_created_at",
                table: "trusted_contacts",
                columns: new[] { "member_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_safety_check_ins");

            migrationBuilder.DropTable(
                name: "meeting_safety_events");

            migrationBuilder.DropTable(
                name: "meeting_safety_links");

            migrationBuilder.DropTable(
                name: "meeting_safety_outbox");

            migrationBuilder.DropTable(
                name: "meeting_safety_recipients");

            migrationBuilder.DropTable(
                name: "meeting_safety_sessions");

            migrationBuilder.DropTable(
                name: "trusted_contacts");
        }
    }
}
