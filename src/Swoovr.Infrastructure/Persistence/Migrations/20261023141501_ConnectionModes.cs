using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConnectionModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "about",
                table: "member_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "member_profiles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "member_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "public_id",
                table: "member_profiles",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "connection_modes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connection_modes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_modes",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    visible = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_modes", x => new { x.account_id, x.mode_id });
                    table.ForeignKey(
                        name: "fk_member_modes_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_modes_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mode_intentions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mode_intentions", x => x.id);
                    table.ForeignKey(
                        name: "fk_mode_intentions_connection_modes_mode_id",
                        column: x => x.mode_id,
                        principalTable: "connection_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_mode_intentions",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    intention_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_mode_intentions", x => new { x.account_id, x.mode_id, x.intention_id });
                    table.ForeignKey(
                        name: "fk_member_mode_intentions_member_modes_account_id_mode_id",
                        columns: x => new { x.account_id, x.mode_id },
                        principalTable: "member_modes",
                        principalColumns: new[] { "account_id", "mode_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_mode_intentions_mode_intentions_intention_id",
                        column: x => x.intention_id,
                        principalTable: "mode_intentions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "connection_modes",
                columns: new[] { "id", "available", "label", "sort_order" },
                values: new object[,]
                {
                    { "activities", true, "Activities", 3 },
                    { "business", true, "Business", 4 },
                    { "dating", true, "Dating", 1 },
                    { "friends", true, "Friends", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_public_id",
                table: "member_profiles",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_mode_intentions_intention_id",
                table: "member_mode_intentions",
                column: "intention_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_modes_mode_id_enabled_visible",
                table: "member_modes",
                columns: new[] { "mode_id", "enabled", "visible" });

            migrationBuilder.CreateIndex(
                name: "ix_mode_intentions_mode_id",
                table: "mode_intentions",
                column: "mode_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_mode_intentions");

            migrationBuilder.DropTable(
                name: "member_modes");

            migrationBuilder.DropTable(
                name: "mode_intentions");

            migrationBuilder.DropTable(
                name: "connection_modes");

            migrationBuilder.DropIndex(
                name: "ix_member_profiles_public_id",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "about",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "area",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "member_profiles");
        }
    }
}
