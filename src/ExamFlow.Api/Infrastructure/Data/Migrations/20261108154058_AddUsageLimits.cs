using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsageLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardsMonthlyQuota",
                table: "LearningAiUsage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserUsageLimits",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyUploads = table.Column<int>(type: "integer", nullable: true),
                    MonthlyExams = table.Column<int>(type: "integer", nullable: true),
                    MonthlyBattles = table.Column<int>(type: "integer", nullable: true),
                    MonthlyAiRequests = table.Column<int>(type: "integer", nullable: true),
                    Unlimited = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUsageLimits", x => x.UserId);
                    table.CheckConstraint("CK_UserUsageLimits_MonthlyAiRequests", "\"MonthlyAiRequests\" IS NULL OR \"MonthlyAiRequests\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_UserUsageLimits_MonthlyBattles", "\"MonthlyBattles\" IS NULL OR \"MonthlyBattles\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_UserUsageLimits_MonthlyExams", "\"MonthlyExams\" IS NULL OR \"MonthlyExams\" BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_UserUsageLimits_MonthlyUploads", "\"MonthlyUploads\" IS NULL OR \"MonthlyUploads\" BETWEEN 0 AND 100000");
                    table.ForeignKey(
                        name: "FK_UserUsageLimits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_ResourceId_Kind",
                table: "UsageEvents",
                columns: new[] { "ResourceId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_UserId_Kind_OccurredAt",
                table: "UsageEvents",
                columns: new[] { "UserId", "Kind", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UserUsageLimits");

            migrationBuilder.DropColumn(
                name: "CountsTowardsMonthlyQuota",
                table: "LearningAiUsage");
        }
    }
}
