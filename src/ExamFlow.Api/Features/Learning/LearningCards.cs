using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

/// <summary>The student's own verdict after seeing the document's answer: "Bilmirdim", "Qismən", "Bilirdim".</summary>
public enum CardGrade { Again, Hard, Good }

/// <summary>
/// Spaced-repetition state of one open question for one student ("Kartlar"). No AI: the student grades themself.
/// A card is identified by its question text (<see cref="CardScheduler.Hash"/>), so renumbering the document keeps the progress.
/// </summary>
public sealed class LearningCardState
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string QuestionHash { get; set; } = "";
    /// <summary>Leitner box 0..<see cref="CardScheduler.MaxBox"/>; higher = known longer.</summary>
    public int Box { get; set; }
    public int Reviews { get; set; }
    public int Lapses { get; set; }
    public CardGrade LastGrade { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset LastReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LearningCardStateConfiguration : IEntityTypeConfiguration<LearningCardState>
{
    public void Configure(EntityTypeBuilder<LearningCardState> builder)
    {
        builder.ToTable("LearningCardStates");
        builder.Property(c => c.QuestionHash).HasMaxLength(64);
        builder.Property(c => c.LastGrade).HasConversion<string>().HasMaxLength(8);
        builder.HasIndex(c => new { c.UserId, c.LearningDocumentId, c.QuestionHash }).IsUnique();
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(c => c.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Leitner-style scheduling: "Bilirdim" moves a card up a box (longer interval), "Bilmirdim" sends it back to the start.</summary>
public static class CardScheduler
{
    public const int MaxBox = 6;
    /// <summary>Cards per study batch, and at most this many new ones in it.</summary>
    public const int BatchSize = 20;
    public const int NewPerBatch = 10;
    /// <summary>Box 3 and above (a week or longer) counts as learned.</summary>
    public const int LearnedBox = 3;

    private static readonly int[] Days = [0, 1, 3, 7, 14, 30, 60];
    // Day intervals end a few hours early, so studying at about the same time the next day finds the card due.
    private static readonly TimeSpan Grace = TimeSpan.FromHours(4);

    public static string Hash(string questionText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(QuizLogic.Key(questionText).Trim())));

    public static (int Box, DateTimeOffset DueAt) Next(int box, CardGrade grade, DateTimeOffset now)
    {
        box = Math.Clamp(box, 0, MaxBox);
        switch (grade)
        {
            case CardGrade.Again:
                return (0, now.AddMinutes(10));
            case CardGrade.Hard:
                // Stays in its box (at least the first one) and comes back after half of that box's interval.
                var hardBox = Math.Max(1, box);
                var half = TimeSpan.FromDays(Days[hardBox] / 2.0);
                return (hardBox, now + (half < TimeSpan.FromHours(12) ? TimeSpan.FromHours(12) : half));
            default:
                var next = Math.Min(MaxBox, box + 1);
                return (next, now + TimeSpan.FromDays(Days[next]) - Grace);
        }
    }
}
