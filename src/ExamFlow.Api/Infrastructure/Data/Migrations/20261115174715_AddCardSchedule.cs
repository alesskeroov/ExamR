using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "LearningCardStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntervalDays",
                table: "LearningCardStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LearningCardStates_UserId_DueAt",
                table: "LearningCardStates",
                columns: new[] { "UserId", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningCardStates_UserId_DueAt",
                table: "LearningCardStates");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "LearningCardStates");

            migrationBuilder.DropColumn(
                name: "IntervalDays",
                table: "LearningCardStates");
        }
    }
}
