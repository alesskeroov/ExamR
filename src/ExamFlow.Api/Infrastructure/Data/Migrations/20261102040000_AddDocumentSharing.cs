using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

public partial class AddDocumentSharing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("ShareWithEveryone", "UploadedDocuments", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.CreateTable("DocumentUserShares", table => new
        {
            DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_DocumentUserShares", x => new { x.DocumentId, x.UserId });
            table.ForeignKey("FK_DocumentUserShares_UploadedDocuments_DocumentId", x => x.DocumentId, "UploadedDocuments", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_DocumentUserShares_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction);
        });
        migrationBuilder.CreateTable("DocumentGroupShares", table => new
        {
            DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_DocumentGroupShares", x => new { x.DocumentId, x.GroupId });
            table.ForeignKey("FK_DocumentGroupShares_UploadedDocuments_DocumentId", x => x.DocumentId, "UploadedDocuments", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_DocumentGroupShares_StudyGroups_GroupId", x => x.GroupId, "StudyGroups", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_DocumentUserShares_UserId", "DocumentUserShares", "UserId");
        migrationBuilder.CreateIndex("IX_DocumentGroupShares_GroupId", "DocumentGroupShares", "GroupId");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("DocumentUserShares");
        migrationBuilder.DropTable("DocumentGroupShares");
        migrationBuilder.DropColumn("ShareWithEveryone", "UploadedDocuments");
    }
}
