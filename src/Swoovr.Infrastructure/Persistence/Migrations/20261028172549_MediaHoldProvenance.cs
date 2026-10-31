using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MediaHoldProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_media_id",
                table: "legal_holds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_source_media_id",
                table: "legal_holds",
                column: "source_media_id",
                unique: true,
                filter: "source_media_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_legal_holds_source_media_id",
                table: "legal_holds");

            migrationBuilder.DropColumn(
                name: "source_media_id",
                table: "legal_holds");
        }
    }
}
