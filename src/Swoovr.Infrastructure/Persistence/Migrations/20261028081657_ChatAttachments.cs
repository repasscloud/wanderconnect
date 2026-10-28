using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "media_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conversation_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "media_items",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_media_id",
                table: "messages",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_items_conversation_id",
                table: "media_items",
                column: "conversation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_media_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_media_items_conversation_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "media_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "media_items");
        }
    }
}
