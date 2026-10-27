using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocationModePrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "share_core_about",
                table: "member_modes",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_profile_versions_account_id_mode_id_captured_at",
                table: "profile_versions",
                columns: new[] { "account_id", "mode_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "ix_search_events_account_id_kind_at",
                table: "search_events",
                columns: new[] { "account_id", "kind", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_search_events_at",
                table: "search_events",
                column: "at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_versions");

            migrationBuilder.DropTable(
                name: "search_events");

            migrationBuilder.DropColumn(
                name: "share_core_about",
                table: "member_modes");
        }
    }
}
