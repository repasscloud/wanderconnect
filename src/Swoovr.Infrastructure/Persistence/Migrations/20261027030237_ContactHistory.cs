using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContactHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    encrypted_value = table.Column<byte[]>(type: "bytea", nullable: false),
                    lookup_hmac = table.Column<byte[]>(type: "bytea", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    change_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_histories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contact_histories_account_id",
                table: "contact_histories",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_contact_histories_contact_type_lookup_hmac",
                table: "contact_histories",
                columns: new[] { "contact_type", "lookup_hmac" });

            migrationBuilder.CreateIndex(
                name: "ix_contact_histories_retain_until",
                table: "contact_histories",
                column: "retain_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_histories");
        }
    }
}
