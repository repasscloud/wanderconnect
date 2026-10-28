using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PushDeviceProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "installation_id",
                table: "push_devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidated_at",
                table: "push_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "token_cipher",
                table: "push_devices",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "token_key_version",
                table: "push_devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attempt_id",
                table: "notification_deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "notification_deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "notification_deliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_devices_installation_id",
                table: "push_devices",
                column: "installation_id",
                unique: true,
                filter: "installation_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_push_devices_installation_id",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "installation_id",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "invalidated_at",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "token_cipher",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "token_key_version",
                table: "push_devices");

            migrationBuilder.DropColumn(
                name: "attempt_id",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "attempts",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "notification_deliveries");
        }
    }
}
