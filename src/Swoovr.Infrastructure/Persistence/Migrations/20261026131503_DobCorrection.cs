using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DobCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dob_check",
                table: "member_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dob_check_at",
                table: "member_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dob_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dob_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_dob_reviews_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dob_reviews_id_verification_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "id_verification_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dob_reviews_attempt_id",
                table: "dob_reviews",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_dob_reviews_created_at",
                table: "dob_reviews",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_dob_reviews_one_open",
                table: "dob_reviews",
                column: "account_id",
                unique: true,
                filter: "resolved_at IS NULL");

            // D166: members approved before this change whose date of birth didn't match get the one-time correction too.
            migrationBuilder.Sql("""
                UPDATE member_profiles p SET dob_check = 'CorrectionNeeded', dob_check_at = now()
                WHERE p.id_status = 'Verified' AND NOT p.date_of_birth_verified
                  AND EXISTS (SELECT 1 FROM id_verification_attempts a WHERE a.account_id = p.account_id AND a.state = 'Approved');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dob_reviews");

            migrationBuilder.DropColumn(
                name: "dob_check",
                table: "member_profiles");

            migrationBuilder.DropColumn(
                name: "dob_check_at",
                table: "member_profiles");
        }
    }
}
