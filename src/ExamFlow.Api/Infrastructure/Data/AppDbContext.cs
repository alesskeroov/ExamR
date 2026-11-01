using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Battles;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<UploadedDocument> Documents => Set<UploadedDocument>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<BattleResult> BattleResults => Set<BattleResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
