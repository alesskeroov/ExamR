using ExamFlow.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamFlow.Api.Features.Learning;

/// <summary>A simple front/back card a student wrote for one document. Only its author sees it.</summary>
public sealed class LearningUserCard
{
    public const int MaxFront = 500;
    public const int MaxBack = 4000;
    /// <summary>Own cards per student and document.</summary>
    public const int MaxPerDocument = 500;
    /// <summary>Prefix of an own card's id, so it never collides with the text hash of a document card.</summary>
    public const string IdPrefix = "u-";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    /// <summary>The deck topic the card belongs to; null = "Mənim kartlarım".</summary>
    public string? TopicKey { get; set; }
    public string Front { get; set; } = "";
    public string Back { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string CardId => IdPrefix + Id.ToString("N");
}

/// <summary>
/// A student's change to one card built from the document: new text, hidden (kept, can be shown again) or deleted (gone for
/// good). The document's deck itself never changes, so other students keep the original card.
/// </summary>
public sealed class LearningCardOverride
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LearningDocumentId { get; set; }
    public string CardId { get; set; } = "";
    public string? Front { get; set; }
    public string? Back { get; set; }
    public bool Hidden { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LearningUserCardConfiguration : IEntityTypeConfiguration<LearningUserCard>
{
    public void Configure(EntityTypeBuilder<LearningUserCard> builder)
    {
        builder.ToTable("LearningUserCards");
        builder.Ignore(c => c.CardId);
        builder.Property(c => c.TopicKey).HasMaxLength(64);
        builder.Property(c => c.Front).HasMaxLength(LearningUserCard.MaxFront);
        builder.Property(c => c.Back).HasMaxLength(LearningUserCard.MaxBack);
        builder.HasIndex(c => new { c.UserId, c.LearningDocumentId });
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(c => c.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LearningCardOverrideConfiguration : IEntityTypeConfiguration<LearningCardOverride>
{
    public void Configure(EntityTypeBuilder<LearningCardOverride> builder)
    {
        builder.ToTable("LearningCardOverrides");
        builder.Property(c => c.CardId).HasMaxLength(64);
        builder.Property(c => c.Front).HasMaxLength(LearningUserCard.MaxFront);
        builder.Property(c => c.Back).HasMaxLength(LearningUserCard.MaxBack);
        builder.HasIndex(c => new { c.UserId, c.LearningDocumentId, c.CardId }).IsUnique();
        builder.HasOne<LearningDocument>().WithMany().HasForeignKey(c => c.LearningDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>One card as one student sees it: the document's card with their changes, or a card they wrote.</summary>
public sealed record PersonalCard(StudyCardItem Card, bool Own, bool Edited, bool Hidden);

public static class PersonalDeck
{
    /// <summary>Topic of own cards written without a topic.</summary>
    public const string OwnTopicKey = "own";
    public const string OwnTopicTitle = "Mənim kartlarım";

    /// <summary>
    /// The document's cards with the student's edits, hidden marks and deletions applied, followed by their own cards.
    /// An edited card becomes a plain front/back card (its lists and options were what the student replaced); it keeps its
    /// id, so its progress stays.
    /// </summary>
    public static List<PersonalCard> Merge(StudyDeck deck, IEnumerable<LearningCardOverride> overrides, IEnumerable<LearningUserCard> own)
    {
        var byCard = overrides.GroupBy(o => o.CardId).ToDictionary(g => g.Key, g => g.First());
        var topics = deck.Topics.Select(t => t.Key).ToHashSet();
        var list = new List<PersonalCard>();
        foreach (var card in deck.Cards)
        {
            if (!byCard.TryGetValue(card.Id, out var change)) { list.Add(new(card, false, false, false)); continue; }
            if (change.Deleted) continue;
            var edited = change.Front is not null || change.Back is not null;
            var shown = edited
                ? card with { Front = change.Front ?? card.Front, Back = change.Back ?? card.Back, Items = [], Others = [], NeedsReview = false }
                : card;
            list.Add(new(shown, false, edited, change.Hidden));
        }
        foreach (var c in own.OrderBy(c => c.CreatedAt))
        {
            var topic = c.TopicKey is { } key && topics.Contains(key) ? key : OwnTopicKey;
            list.Add(new(new StudyCardItem(c.CardId, CardKind.Custom, topic, c.Front, c.Back, [], [], 0, null, 0), true, false, false));
        }
        return list;
    }
}
