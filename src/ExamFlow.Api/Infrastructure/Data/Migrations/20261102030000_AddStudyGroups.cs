using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

public partial class AddStudyGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudyGroups",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_100_BIN2"),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_StudyGroups", x => x.Id));
        migrationBuilder.CreateTable(
            name: "StudyGroupMembers",
            columns: table => new
            {
                GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudyGroupMembers", x => new { x.GroupId, x.UserId });
                table.ForeignKey("FK_StudyGroupMembers_StudyGroups_GroupId", x => x.GroupId, "StudyGroups", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_StudyGroupMembers_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction);
            });
        migrationBuilder.CreateIndex("IX_StudyGroups_NormalizedName", "StudyGroups", "NormalizedName", unique: true);
        migrationBuilder.CreateIndex("IX_StudyGroupMembers_UserId", "StudyGroupMembers", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("StudyGroupMembers");
        migrationBuilder.DropTable("StudyGroups");
    }
}
