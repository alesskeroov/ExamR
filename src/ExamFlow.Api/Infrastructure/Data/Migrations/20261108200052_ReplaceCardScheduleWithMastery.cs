using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCardScheduleWithMastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "LearningCardStates");

            migrationBuilder.RenameColumn(
                name: "Box",
                table: "LearningCardStates",
                newName: "Streak");

            migrationBuilder.AddColumn<bool>(
                name: "Mastered",
                table: "LearningCardStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Leitner boxes are not streaks: keep only "the last answer was Bilirdim" as a streak of 1.
            migrationBuilder.Sql("UPDATE \"LearningCardStates\" SET \"Streak\" = CASE WHEN \"LastGrade\" = 'Good' THEN 1 ELSE 0 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mastered",
                table: "LearningCardStates");

            migrationBuilder.RenameColumn(
                name: "Streak",
                table: "LearningCardStates",
                newName: "Box");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "LearningCardStates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
