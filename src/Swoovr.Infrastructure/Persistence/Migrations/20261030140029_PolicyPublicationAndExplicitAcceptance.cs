using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PolicyPublicationAndExplicitAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_acceptances",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_acceptances", x => new { x.account_id, x.version });
                    table.CheckConstraint("ck_policy_acceptance", "version ~ '^[A-Za-z0-9][A-Za-z0-9.-]{0,39}$' AND content_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_policy_acceptances_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_notice_publications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    link = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    staged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    staged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    send_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_notice_publications", x => x.id);
                    table.CheckConstraint("ck_policy_publication", "version ~ '^[A-Za-z0-9][A-Za-z0-9.-]{0,39}$' AND length(trim(title)) BETWEEN 1 AND 120 AND title !~ '[[:cntrl:]]' AND link IN ('/safety','/safety/advice') AND ((published_at IS NULL AND published_by IS NULL AND send_until IS NULL AND purge_after IS NULL AND ((disposed_at IS NULL AND staged_by IS NOT NULL) OR (disposed_at IS NOT NULL AND disposed_at>=staged_at+interval '1440 hours' AND staged_by IS NULL AND title='Expired safety update' AND link='/safety'))) OR (published_at IS NOT NULL AND send_until IS NOT NULL AND purge_after IS NOT NULL AND published_at>=staged_at AND send_until=published_at+interval '720 hours' AND purge_after=published_at+interval '1440 hours' AND ((disposed_at IS NULL AND staged_by IS NOT NULL AND published_by IS NOT NULL) OR (disposed_at IS NOT NULL AND disposed_at>=purge_after AND staged_by IS NULL AND published_by IS NULL AND title='Expired safety update' AND link='/safety'))))");
                });

            migrationBuilder.CreateTable(
                name: "policy_notice_recipients",
                columns: table => new
                {
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_notice_recipients", x => new { x.publication_id, x.account_id });
                    table.CheckConstraint("ck_policy_recipient", "(status='queued' AND completed_at IS NULL) OR (status IN ('centre-recorded','suppressed') AND completed_at IS NOT NULL AND completed_at>=created_at)");
                    table.ForeignKey(
                        name: "fk_policy_notice_recipients_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_policy_notice_recipients_policy_notice_publications_publica~",
                        column: x => x.publication_id,
                        principalTable: "policy_notice_publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policy_notice_publications_purge_after",
                table: "policy_notice_publications",
                column: "purge_after");

            migrationBuilder.CreateIndex(
                name: "ix_policy_notice_publications_version",
                table: "policy_notice_publications",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_notice_recipients_account_id",
                table: "policy_notice_recipients",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_notice_recipients_publication_id_status_account_id",
                table: "policy_notice_recipients",
                columns: new[] { "publication_id", "status", "account_id" });
            migrationBuilder.Sql(PolicyNoticeGuardSql.Up);
            migrationBuilder.Sql(PolicyAcceptanceGuardSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PolicyAcceptanceGuardSql.Down);
            migrationBuilder.Sql(PolicyNoticeGuardSql.Down);
            migrationBuilder.DropTable(
                name: "policy_acceptances");

            migrationBuilder.DropTable(
                name: "policy_notice_recipients");

            migrationBuilder.DropTable(
                name: "policy_notice_publications");
        }
    }
}
