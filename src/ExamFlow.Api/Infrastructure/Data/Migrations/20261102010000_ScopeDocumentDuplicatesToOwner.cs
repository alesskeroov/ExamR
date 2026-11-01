using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102010000_ScopeDocumentDuplicatesToOwner")]
public sealed class ScopeDocumentDuplicatesToOwner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("UX_UploadedDocuments_Sha256", "UploadedDocuments");
        migrationBuilder.CreateIndex("UX_UploadedDocuments_User_Sha256", "UploadedDocuments",
            new[] { "UploadedByUserId", "Sha256" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("UX_UploadedDocuments_User_Sha256", "UploadedDocuments");
        migrationBuilder.CreateIndex("UX_UploadedDocuments_Sha256", "UploadedDocuments",
            "Sha256", unique: true);
    }
}
