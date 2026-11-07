using ExamFlow.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamFlow.Api.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261102050000_AddAdminRoleAudits")]
public partial class AddAdminRoleAudits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("AdminRoleAudits", table => new
        {
            Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_AdminRoleAudits", x => x.Id));
        migrationBuilder.CreateIndex("IX_AdminRoleAudits_TargetUserId", "AdminRoleAudits", "TargetUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AdminRoleAudits");
}
