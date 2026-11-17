using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuideLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonthlyGuides",
                table: "UserUsageLimits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyGuides",
                table: "Plans",
                type: "integer",
                nullable: true);

            // Packages created by admins (not seeded) start without the guide; NULL would mean unlimited.
            migrationBuilder.Sql("""
                UPDATE "Plans" SET "MonthlyGuides" = 0
                WHERE "Id" NOT IN ('6a1f0c3e-0000-4000-8000-000000000001', '6a1f0c3e-0000-4000-8000-000000000002',
                    '6a1f0c3e-0000-4000-8000-000000000003', '6a1f0c3e-0000-4000-8000-000000000004', '6a1f0c3e-0000-4000-8000-000000000005');
                """);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("6a1f0c3e-0000-4000-8000-000000000001"),
                column: "MonthlyGuides",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("6a1f0c3e-0000-4000-8000-000000000002"),
                column: "MonthlyGuides",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("6a1f0c3e-0000-4000-8000-000000000003"),
                column: "MonthlyGuides",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("6a1f0c3e-0000-4000-8000-000000000004"),
                column: "MonthlyGuides",
                value: 10);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("6a1f0c3e-0000-4000-8000-000000000005"),
                column: "MonthlyGuides",
                value: null);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserUsageLimits_MonthlyGuides",
                table: "UserUsageLimits",
                sql: "\"MonthlyGuides\" IS NULL OR \"MonthlyGuides\" BETWEEN 0 AND 100000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_MonthlyGuides",
                table: "Plans",
                sql: "\"MonthlyGuides\" IS NULL OR \"MonthlyGuides\" BETWEEN 0 AND 100000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserUsageLimits_MonthlyGuides",
                table: "UserUsageLimits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plans_MonthlyGuides",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyGuides",
                table: "UserUsageLimits");

            migrationBuilder.DropColumn(
                name: "MonthlyGuides",
                table: "Plans");
        }
    }
}
