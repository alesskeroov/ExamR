using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102080000_AddDataProtectionKeys")]
public partial class AddDataProtectionKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("DataProtectionKeys", table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
            Xml = table.Column<string>(type: "nvarchar(max)", nullable: true),
        }, constraints: table => table.PrimaryKey("PK_DataProtectionKeys", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("DataProtectionKeys");
}
