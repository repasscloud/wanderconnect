using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BetaDistributionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ios_distribution_changed_at",
                table: "beta_enrolments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ios_distribution_changed_by",
                table: "beta_enrolments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ios_distribution_revision",
                table: "beta_enrolments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ios_distribution_state",
                table: "beta_enrolments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "not-recorded");

            migrationBuilder.AddCheckConstraint(
                name: "ck_beta_ios_record",
                table: "beta_enrolments",
                sql: "ios_distribution_state IN ('not-recorded','not-invited','invited','accepted','removed') AND ios_distribution_revision>=0 AND ((ios_distribution_revision=0 AND ios_distribution_changed_at IS NULL AND ios_distribution_changed_by IS NULL) OR (ios_distribution_revision>0 AND ios_distribution_changed_at IS NOT NULL AND ios_distribution_changed_by IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_beta_ios_record",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "ios_distribution_changed_at",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "ios_distribution_changed_by",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "ios_distribution_revision",
                table: "beta_enrolments");

            migrationBuilder.DropColumn(
                name: "ios_distribution_state",
                table: "beta_enrolments");
        }
    }
}
