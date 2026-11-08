using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

/// <summary>The student's own verdict after seeing the document's answer: "Bilmirdim", "Qismən", "Bilirdim".</summary>
public enum CardGrade { Again, Hard, Good }

/// <summary>
/// Learning state of one card for one student. No AI and no dates: a card is mastered after
/// <see cref="CardMastery.Required"/> "Bilirdim" in a row. Identified by its question text (<see cref="CardMastery.Hash"/>),
/// so renumbering the document keeps the progress.
/// </summary>
public sealed class LearningCardState
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string QuestionHash { get; set; } = "";
    /// <summary>"Bilirdim" answers in a row; any other grade resets it.</summary>
    public int Streak { get; set; }
    public bool Mastered { get; set; }
    public int Reviews { get; set; }
    /// <summary>How often the student answered "Bilmirdim".</summary>
    public int Lapses { get; set; }
    public CardGrade LastGrade { get; set; }
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

public static class CardMastery
{
    /// <summary>"Bilirdim" answers in a row that make a card mastered.</summary>
    public const int Required = 2;

    public static string Hash(string questionText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(QuizLogic.Key(questionText).Trim())));

    public static (int Streak, bool Mastered) Next(int streak, CardGrade grade) =>
        grade == CardGrade.Good ? (streak + 1, streak + 1 >= Required) : (0, false);
}

public sealed record CardTopic(string Key, string Title, IReadOnlyList<int> Numbers);

/// <summary>
/// Topics without AI. Real headings of the document are used when they group the questions well; otherwise the questions
/// are split in document order into blocks of <see cref="BlockSize"/> ("Suallar 1–10").
/// </summary>
public static class CardTopics
{
    public const int BlockSize = 10;
    private const int MaxTopicSize = 15;
    private const int MaxHeadingLength = 80;

    /// <param name="sectionOf">Heading of the chunk a question starts in (null when the document has none there).</param>
    public static IReadOnlyList<CardTopic> Build(IReadOnlyList<IndexedQuestion> questions, Func<IndexedQuestion, string?> sectionOf)
    {
        if (questions.Count == 0) return [];
        var questionTexts = questions.Select(q => Fold(q.Text)).ToHashSet();
        string? Heading(IndexedQuestion q)
        {
            var section = sectionOf(q)?.Trim();
            if (string.IsNullOrEmpty(section) || section.Length > MaxHeadingLength) return null;
            var folded = Fold(section);
            // A "heading" that is one of the questions (or contains one) is the question itself, not a topic.
            return questionTexts.Any(t => t.Length > 0 && (folded.Contains(t) || t.Contains(folded))) ? null : section;
        }

        var headings = questions.Select(Heading).ToList();
        var distinct = headings.OfType<string>().Distinct().Count();
        var covered = headings.Count(h => h is not null);
        if (distinct >= 2 && distinct <= questions.Count / 2 && covered >= questions.Count * 0.7)
        {
            var topics = new List<(string Title, List<int> Numbers)>();
            for (var i = 0; i < questions.Count; i++)
            {
                var title = headings[i] ?? (topics.Count > 0 ? topics[^1].Title : "Giriş");
                if (topics.Count == 0 || topics[^1].Title != title) topics.Add((title, []));
                topics[^1].Numbers.Add(questions[i].Number);
            }
            return topics.SelectMany(t =>
            {
                var parts = t.Numbers.Chunk(MaxTopicSize).ToList();
                return parts.Select((p, i) => Topic(parts.Count > 1 ? $"{t.Title} ({i + 1}/{parts.Count})" : t.Title, p));
            }).ToList();
        }

        return questions.Chunk(BlockSize)
            .Select(block => Topic(block.Length == 1 ? $"Sual {block[0].Number}" : $"Suallar {block[0].Number}–{block[^1].Number}", block.Select(q => q.Number)))
            .ToList();
    }

    private static CardTopic Topic(string title, IEnumerable<int> numbers)
    {
        var list = numbers.ToList();
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(',', list))))[..12].ToLowerInvariant();
        return new(key, title, list);
    }

    private static string Fold(string text) => TextNormalizer.FoldedPhrase(text).Trim();
}
