using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExamQuestionRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RangeFrom",
                table: "Exams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RangeTo",
                table: "Exams",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RangeFrom",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "RangeTo",
                table: "Exams");
        }
    }
}
