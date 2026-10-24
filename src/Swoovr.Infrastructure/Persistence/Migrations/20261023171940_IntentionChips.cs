using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Swoovr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntentionChips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "mode_intentions",
                columns: new[] { "id", "available", "label", "mode_id", "sort_order" },
                values: new object[,]
                {
                    { new Guid("00000016-0001-4000-8000-000000000001"), true, "Long-term relationship", "dating", 1 },
                    { new Guid("00000016-0001-4000-8000-000000000002"), true, "Short-term dating", "dating", 2 },
                    { new Guid("00000016-0001-4000-8000-000000000003"), true, "Casual dating", "dating", 3 },
                    { new Guid("00000016-0001-4000-8000-000000000004"), true, "Something casual / no strings", "dating", 4 },
                    { new Guid("00000016-0001-4000-8000-000000000005"), true, "Friends with benefits", "dating", 5 },
                    { new Guid("00000016-0001-4000-8000-000000000006"), true, "Open to exploring", "dating", 6 },
                    { new Guid("00000016-0001-4000-8000-000000000007"), true, "Marriage / life partner", "dating", 7 },
                    { new Guid("00000016-0001-4000-8000-000000000008"), true, "Intimacy / physical connection", "dating", 8 },
                    { new Guid("00000016-0001-4000-8000-000000000009"), true, "Not sure yet", "dating", 9 },
                    { new Guid("00000016-0001-4000-8000-000000000010"), true, "New connections / see where it goes", "dating", 10 },
                    { new Guid("00000016-0002-4000-8000-000000000001"), true, "New friendships", "friends", 1 },
                    { new Guid("00000016-0002-4000-8000-000000000002"), true, "Close / meaningful friendships", "friends", 2 },
                    { new Guid("00000016-0002-4000-8000-000000000003"), true, "Casual friendships", "friends", 3 },
                    { new Guid("00000016-0002-4000-8000-000000000004"), true, "Social groups / meetups", "friends", 4 },
                    { new Guid("00000016-0002-4000-8000-000000000005"), true, "Activity partners", "friends", 5 },
                    { new Guid("00000016-0002-4000-8000-000000000006"), true, "Networking / meeting people", "friends", 6 },
                    { new Guid("00000016-0002-4000-8000-000000000007"), true, "Emotional support / someone to talk to", "friends", 7 },
                    { new Guid("00000016-0002-4000-8000-000000000008"), true, "Travel companions", "friends", 8 },
                    { new Guid("00000016-0002-4000-8000-000000000009"), true, "Local connections", "friends", 9 },
                    { new Guid("00000016-0002-4000-8000-000000000010"), true, "Expanding my social circle", "friends", 10 },
                    { new Guid("00000016-0003-4000-8000-000000000001"), true, "Finding activity partners", "activities", 1 },
                    { new Guid("00000016-0003-4000-8000-000000000002"), true, "Group outings", "activities", 2 },
                    { new Guid("00000016-0003-4000-8000-000000000003"), true, "Exploring local events", "activities", 3 },
                    { new Guid("00000016-0003-4000-8000-000000000004"), true, "Trying new experiences", "activities", 4 },
                    { new Guid("00000016-0003-4000-8000-000000000005"), true, "Fitness and outdoor activities", "activities", 5 },
                    { new Guid("00000016-0003-4000-8000-000000000006"), true, "Food and dining experiences", "activities", 6 },
                    { new Guid("00000016-0003-4000-8000-000000000007"), true, "Arts, culture and entertainment", "activities", 7 },
                    { new Guid("00000016-0003-4000-8000-000000000008"), true, "Weekend adventures", "activities", 8 },
                    { new Guid("00000016-0003-4000-8000-000000000009"), true, "Travel and sightseeing", "activities", 9 },
                    { new Guid("00000016-0003-4000-8000-000000000010"), true, "Spontaneous plans", "activities", 10 },
                    { new Guid("00000016-0004-4000-8000-000000000001"), true, "Professional networking", "business", 1 },
                    { new Guid("00000016-0004-4000-8000-000000000002"), true, "Business partnerships", "business", 2 },
                    { new Guid("00000016-0004-4000-8000-000000000003"), true, "Finding clients / customers", "business", 3 },
                    { new Guid("00000016-0004-4000-8000-000000000004"), true, "Finding service providers", "business", 4 },
                    { new Guid("00000016-0004-4000-8000-000000000005"), true, "Mentorship / guidance", "business", 5 },
                    { new Guid("00000016-0004-4000-8000-000000000006"), true, "Career opportunities", "business", 6 },
                    { new Guid("00000016-0004-4000-8000-000000000007"), true, "Collaboration / projects", "business", 7 },
                    { new Guid("00000016-0004-4000-8000-000000000008"), true, "Entrepreneurship / startups", "business", 8 },
                    { new Guid("00000016-0004-4000-8000-000000000009"), true, "Industry events / meetups", "business", 9 },
                    { new Guid("00000016-0004-4000-8000-000000000010"), true, "Knowledge sharing", "business", 10 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0001-4000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0002-4000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0003-4000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "mode_intentions",
                keyColumn: "id",
                keyValue: new Guid("00000016-0004-4000-8000-000000000010"));
        }
    }
}
