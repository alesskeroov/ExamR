using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102070000_AddFocusSessions")]
public partial class AddFocusSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("FocusSessions", table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Activity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            PlannedMinutes = table.Column<int>(type: "int", nullable: false),
            FocusSeconds = table.Column<int>(type: "int", nullable: false),
            Answered = table.Column<int>(type: "int", nullable: false),
            Correct = table.Column<int>(type: "int", nullable: false),
            Completed = table.Column<bool>(type: "bit", nullable: false),
            StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_FocusSessions", x => x.Id);
            table.ForeignKey("FK_FocusSessions_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_FocusSessions_UserId_EndedAt", "FocusSessions", new[] { "UserId", "EndedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("FocusSessions");
}
