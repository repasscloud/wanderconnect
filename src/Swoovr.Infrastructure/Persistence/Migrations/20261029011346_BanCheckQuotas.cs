using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BanCheckQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ban_check_attempts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caller_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    address_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    key_version = table.Column<int>(type: "integer", nullable: false),
                    identifier_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_ban_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ban_check_attempts", x => x.id);
                    table.CheckConstraint("ck_ban_attempt_hash", "octet_length(caller_hash) = 32 AND (address_hash IS NULL OR octet_length(address_hash) = 32) AND key_version > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ban_check_attempts_address_hash_key_version_at",
                table: "ban_check_attempts",
                columns: new[] { "address_hash", "key_version", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_ban_check_attempts_at",
                table: "ban_check_attempts",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_ban_check_attempts_caller_hash_key_version_identifier_type_~",
                table: "ban_check_attempts",
                columns: new[] { "caller_hash", "key_version", "identifier_type", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_ban_check_attempts_matched_ban_ids",
                table: "ban_check_attempts",
                column: "matched_ban_ids")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ban_check_attempts");
        }
    }
}
