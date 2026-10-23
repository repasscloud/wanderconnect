using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FounderTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "founder_place",
                table: "member_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "founder_places",
                columns: table => new
                {
                    place = table.Column<int>(type: "integer", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_founder_places", x => x.place);
                    table.CheckConstraint("ck_founder_places_place", "place >= 1");
                });

            migrationBuilder.CreateTable(
                name: "founder_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    place = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    secret_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidated_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_founder_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_founder_tokens_founder_places_place",
                        column: x => x.place,
                        principalTable: "founder_places",
                        principalColumn: "place",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_founder_place",
                table: "member_profiles",
                column: "founder_place",
                unique: true,
                filter: "founder_place IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_founder_tokens_code",
                table: "founder_tokens",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_founder_tokens_place",
                table: "founder_tokens",
                column: "place");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "founder_tokens");

            migrationBuilder.DropTable(
                name: "founder_places");

            migrationBuilder.DropIndex(
                name: "ix_member_profiles_founder_place",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "founder_place",
                table: "member_profiles");
        }
    }
}
