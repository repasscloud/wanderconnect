using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegalHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    release_conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    placed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    placed_by_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_due = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_holds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custody_events",
                columns: table => new
                {
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    previous_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custody_events", x => x.sequence);
                    table.ForeignKey(
                        name: "fk_custody_events_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disclosures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    officer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verified_how = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    disclosed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    disclosed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disclosures", x => x.id);
                    table.ForeignKey(
                        name: "fk_disclosures_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_exports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    downloaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    disclosure_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_exports", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_exports_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    record_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captured_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    @sealed = table.Column<byte[]>(name: "sealed", type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_items_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "legal_hold_scopes",
                columns: table => new
                {
                    hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_hold_scopes", x => new { x.hold_id, x.kind, x.target_id });
                    table.ForeignKey(
                        name: "fk_legal_hold_scopes_legal_holds_hold_id",
                        column: x => x.hold_id,
                        principalTable: "legal_holds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custody_events_hold_id_sequence",
                table: "custody_events",
                columns: new[] { "hold_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_disclosures_hold_id",
                table: "disclosures",
                column: "hold_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_exports_hold_id",
                table: "evidence_exports",
                column: "hold_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_exports_token_hash",
                table: "evidence_exports",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_items_hold_id_kind_record_id",
                table: "evidence_items",
                columns: new[] { "hold_id", "kind", "record_id" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_hold_scopes_target_id",
                table: "legal_hold_scopes",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_case_id",
                table: "legal_holds",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_status_review_due",
                table: "legal_holds",
                columns: new[] { "status", "review_due" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custody_events");

            migrationBuilder.DropTable(
                name: "disclosures");

            migrationBuilder.DropTable(
                name: "evidence_exports");

            migrationBuilder.DropTable(
                name: "evidence_items");

            migrationBuilder.DropTable(
                name: "legal_hold_scopes");

            migrationBuilder.DropTable(
                name: "legal_holds");
        }
    }
}
