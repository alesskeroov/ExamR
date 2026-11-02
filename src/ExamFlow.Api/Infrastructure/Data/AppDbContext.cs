using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Study;
using ExamFlow.Api.Features.Battles;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Friends;
using ExamFlow.Api.Features.Groups;
using ExamFlow.Api.Features.Questions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<UploadedDocument> Documents => Set<UploadedDocument>();
    public DbSet<DocumentUserShare> DocumentUserShares => Set<DocumentUserShare>();
    public DbSet<DocumentGroupShare> DocumentGroupShares => Set<DocumentGroupShare>();
    public DbSet<Question> Questions => Set<Question>();
    // Data Protection keys survive restarts of hosts with an ephemeral disk (Render).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<QuestionBookmark> QuestionBookmarks => Set<QuestionBookmark>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<BattleRating> BattleRatings => Set<BattleRating>();
    public DbSet<BattleResult> BattleResults => Set<BattleResult>();
    public DbSet<StoredBattleRoom> StoredBattleRooms => Set<StoredBattleRoom>();
    public DbSet<StoredBattleMember> StoredBattleMembers => Set<StoredBattleMember>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<SocialNotification> SocialNotifications => Set<SocialNotification>();

    public DbSet<AdminRoleAudit> AdminRoleAudits => Set<AdminRoleAudit>();
    public DbSet<StudyGroup> StudyGroups => Set<StudyGroup>();
    public DbSet<StudyGroupMember> StudyGroupMembers => Set<StudyGroupMember>();

    public DbSet<StudyAiTurn> StudyAiTurns => Set<StudyAiTurn>();
    public DbSet<StudyAttempt> StudyAttempts => Set<StudyAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>().HasIndex(u => u.NormalizedNickname).IsUnique();
        builder.Entity<BattleRating>().HasOne(r => r.User).WithOne().HasForeignKey<BattleRating>(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<BattleRating>().HasIndex(r => new { r.CurrentRating, r.Wins }).IsDescending(true, true);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
