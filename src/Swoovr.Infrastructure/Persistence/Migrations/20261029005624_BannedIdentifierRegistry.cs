using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BannedIdentifierRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banned_identifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    identifier_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<int>(type: "integer", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ban_reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    banned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ban_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    last_matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    match_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banned_identifiers", x => x.id);
                    table.CheckConstraint("ck_ban_count", "match_count >= 0");
                    table.CheckConstraint("ck_ban_expired", "status <> 'Expired' OR ban_expires_at IS NOT NULL");
                    table.CheckConstraint("ck_ban_hash", "octet_length(identifier_hash) = 32 AND key_version > 0");
                    table.CheckConstraint("ck_ban_kind", "identifier_type IN ('Email', 'Mobile') AND status IN ('Active', 'Revoked', 'Expired')");
                    table.CheckConstraint("ck_ban_reason", "ban_reason_code ~ '^[a-z][a-z0-9-]{1,63}$'");
                    table.CheckConstraint("ck_ban_revoked", "status <> 'Revoked' OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND ban_expires_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_banned_identifiers_moderation_cases_case_id",
                        column: x => x.case_id,
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ban_transitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    banned_identifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    new_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    new_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: false),
                    new_values = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_transitions", x => x.id);
                    table.CheckConstraint("ck_ban_transition_reason", "length(btrim(reason)) BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "fk_ban_transitions_banned_identifiers_banned_identifier_id",
                        column: x => x.banned_identifier_id,
                        principalTable: "banned_identifiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ban_case_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_case_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_ban_case_notes_ban_transitions_transition_id",
                        column: x => x.transition_id,
                        principalTable: "ban_transitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ban_case_notes_case_id_at",
                table: "ban_case_notes",
                columns: new[] { "case_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_ban_case_notes_transition_id",
                table: "ban_case_notes",
                column: "transition_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_transitions_banned_identifier_id",
                table: "ban_transitions",
                column: "banned_identifier_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_transitions_case_id_at",
                table: "ban_transitions",
                columns: new[] { "case_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_case_id",
                table: "banned_identifiers",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_identifier_type_key_version_identifier_h~",
                table: "banned_identifiers",
                columns: new[] { "identifier_type", "key_version", "identifier_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_status_ban_expires_at",
                table: "banned_identifiers",
                columns: new[] { "status", "ban_expires_at" });
            // Restricted transition evidence and associated notes are immutable. Retention disposal requires a
            // separately reviewed bounded authority; ordinary application SQL cannot rewrite or erase them.
            migrationBuilder.Sql("""
                CREATE FUNCTION ban_history_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'ban history is append-only'; END $$;
                CREATE TRIGGER ban_transitions_no_change BEFORE UPDATE OR DELETE ON ban_transitions
                    FOR EACH ROW EXECUTE FUNCTION ban_history_append_only();
                CREATE TRIGGER ban_transitions_no_truncate BEFORE TRUNCATE ON ban_transitions
                    FOR EACH STATEMENT EXECUTE FUNCTION ban_history_append_only();
                CREATE TRIGGER ban_case_notes_no_change BEFORE UPDATE OR DELETE ON ban_case_notes
                    FOR EACH ROW EXECUTE FUNCTION ban_history_append_only();
                CREATE TRIGGER ban_case_notes_no_truncate BEFORE TRUNCATE ON ban_case_notes
                    FOR EACH STATEMENT EXECUTE FUNCTION ban_history_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS ban_transitions_no_change ON ban_transitions;
                DROP TRIGGER IF EXISTS ban_transitions_no_truncate ON ban_transitions;
                DROP TRIGGER IF EXISTS ban_case_notes_no_change ON ban_case_notes;
                DROP TRIGGER IF EXISTS ban_case_notes_no_truncate ON ban_case_notes;
                DROP FUNCTION IF EXISTS ban_history_append_only();
                """);

            migrationBuilder.DropTable(
                name: "ban_case_notes");

            migrationBuilder.DropTable(
                name: "ban_transitions");

            migrationBuilder.DropTable(
                name: "banned_identifiers");
        }
    }
}
