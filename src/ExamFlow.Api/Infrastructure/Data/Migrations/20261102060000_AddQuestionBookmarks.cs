using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102060000_AddQuestionBookmarks")]
public partial class AddQuestionBookmarks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("QuestionBookmarks", table => new
        {
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_QuestionBookmarks", x => new { x.UserId, x.QuestionId });
            table.ForeignKey("FK_QuestionBookmarks_Questions_QuestionId", x => x.QuestionId, "Questions", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_QuestionBookmarks_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction);
        });
        migrationBuilder.CreateIndex("IX_QuestionBookmarks_QuestionId", "QuestionBookmarks", "QuestionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("QuestionBookmarks");
}
