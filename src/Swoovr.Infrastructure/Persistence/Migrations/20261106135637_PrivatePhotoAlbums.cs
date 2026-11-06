using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivatePhotoAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "album_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "album_only",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "explicit_media_preferences",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    receive = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_explicit_media_preferences", x => new { x.account_id, x.mode_id });
                });

            migrationBuilder.CreateTable(
                name: "photo_albums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_albums", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "photo_album_items",
                columns: table => new
                {
                    album_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_album_items", x => new { x.album_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_photo_album_items_media_items_media_id",
                        column: x => x.media_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_photo_album_items_photo_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "photo_albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_album_modes",
                columns: table => new
                {
                    album_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_album_modes", x => new { x.album_id, x.mode_id });
                    table.ForeignKey(
                        name: "fk_photo_album_modes_photo_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "photo_albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_album_items_media_id",
                table: "photo_album_items",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_albums_account_id_created_at",
                table: "photo_albums",
                columns: new[] { "account_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "explicit_media_preferences");

            migrationBuilder.DropTable(
                name: "photo_album_items");

            migrationBuilder.DropTable(
                name: "photo_album_modes");

            migrationBuilder.DropTable(
                name: "photo_albums");

            migrationBuilder.DropColumn(
                name: "album_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "album_only",
                table: "media_items");
        }
    }
}
