using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "restricted");

            migrationBuilder.CreateTable(
                name: "identity_accesses",
                schema: "restricted",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_accesses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terminated_phone_holds",
                columns: table => new
                {
                    phone_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terminated_phone_holds", x => x.phone_hash);
                });

            migrationBuilder.CreateTable(
                name: "verified_identities",
                schema: "restricted",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    issuing_country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    @sealed = table.Column<byte[]>(name: "sealed", type: "bytea", nullable: false),
                    match_hmac = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verified_identities", x => x.account_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_identity_accesses_account_id_at",
                schema: "restricted",
                table: "identity_accesses",
                columns: new[] { "account_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_terminated_phone_holds_expires_at",
                table: "terminated_phone_holds",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_verified_identities_match_hmac",
                schema: "restricted",
                table: "verified_identities",
                column: "match_hmac");

            migrationBuilder.CreateIndex(
                name: "ix_verified_identities_retain_until",
                schema: "restricted",
                table: "verified_identities",
                column: "retain_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_accesses",
                schema: "restricted");

            migrationBuilder.DropTable(
                name: "terminated_phone_holds");

            migrationBuilder.DropTable(
                name: "verified_identities",
                schema: "restricted");
        }
    }
}
