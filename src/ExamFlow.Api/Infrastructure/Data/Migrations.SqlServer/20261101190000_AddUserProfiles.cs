using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261101190000_AddUserProfiles")]
public sealed class AddUserProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Nickname", "AspNetUsers", type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("NormalizedNickname", "AspNetUsers", type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("AvatarFileName", "AspNetUsers", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("AvatarContentType", "AspNetUsers", type: "nvarchar(32)", maxLength: 32, nullable: true);
        migrationBuilder.Sql("UPDATE AspNetUsers SET Nickname = 'user' + LEFT(REPLACE(CONVERT(varchar(36), Id), '-', ''), 16), NormalizedNickname = 'USER' + LEFT(REPLACE(CONVERT(varchar(36), Id), '-', ''), 16)");
        migrationBuilder.CreateIndex("IX_AspNetUsers_NormalizedNickname", "AspNetUsers", "NormalizedNickname", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_AspNetUsers_NormalizedNickname", "AspNetUsers");
        migrationBuilder.DropColumn("Nickname", "AspNetUsers");
        migrationBuilder.DropColumn("NormalizedNickname", "AspNetUsers");
        migrationBuilder.DropColumn("AvatarFileName", "AspNetUsers");
        migrationBuilder.DropColumn("AvatarContentType", "AspNetUsers");
    }
}
