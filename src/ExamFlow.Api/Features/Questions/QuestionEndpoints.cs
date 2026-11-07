using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Features.Auth;
using ExamFlow.Api.Features.Documents;
using ExamFlow.Api.Features.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Questions;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/questions").RequireAuthorization();
        group.MapGet("/", List);
        group.MapGet("/stats", Stats);
        group.MapPut("/{id:guid}", Update).Validate<UpdateQuestionRequest>();
        group.MapPost("/{id:guid}/verify", Verify);
        group.MapPut("/{id:guid}/bookmark", (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => Bookmark(id, true, user, db, ct));
        group.MapDelete("/{id:guid}/bookmark", (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => Bookmark(id, false, user, db, ct));
    }

    private static IQueryable<Question> Readable(AppDbContext db, Guid userId)
    {
        var readable = DocumentAccess.Readable(db, userId);
        return db.Questions.Where(q => readable.Any(d => d.Id == q.DocumentId));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, AppDbContext db, TimeProvider clock, IOptions<ExamOptions> examOptions, CancellationToken ct,
        Guid? documentId = null, VerificationStatus? status = null, bool? hasWarning = null,
        string? search = null, string? type = null, bool? bookmarked = null, bool? wrong = null,
        int? page = null, int? pageSize = null)
    {
        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var userId = user.GetUserId();
        if (documentId is not null && !await DocumentAccess.Readable(db, userId)
            .AnyAsync(d => d.Id == documentId, ct))
            return TypedResults.Problem("Sənəd tapılmadı.", statusCode: 404);
        var query = Readable(db, userId).AsNoTracking();

        // The bank shows correct answers, so the PDF of a running exam stays hidden until the exam ends
        // (same rule as Study practice); otherwise the reveal rules could be bypassed from here.
        // Answers are still accepted during the grace period after the deadline, so the PDF stays hidden until then.
        var answersOpenSince = clock.GetUtcNow().AddSeconds(-examOptions.Value.AnswerGraceSeconds);
        var (allHidden, hidden) = await ActiveExamDocumentsAsync(db, userId, answersOpenSince, ct);
        if (allHidden || (documentId is { } requested && hidden.Contains(requested)))
            return TypedResults.Problem("Bu sənəd üzrə imtahanı bitirdikdən sonra suallara baxa bilərsiniz.", statusCode: 409);
        if (hidden.Count > 0)
        {
            query = query.Where(q => !hidden.Contains(q.DocumentId));
        }

        if (documentId is not null)
        {
            query = query.Where(q => q.DocumentId == documentId);
        }

        if (status is not null)
        {
            query = query.Where(q => q.VerificationStatus == status);
        }

        if (hasWarning is not null)
        {
            query = hasWarning.Value ? query.Where(q => q.ParseWarning != null) : query.Where(q => q.ParseWarning == null);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(q => q.Text.Contains(term));
        }

        if (type == "choice") query = query.Where(q => q.Options.Any());
        else if (type == "open") query = query.Where(q => !q.Options.Any());

        if (bookmarked == true)
        {
            query = query.Where(q => db.QuestionBookmarks.Any(b => b.QuestionId == q.Id && b.UserId == userId));
        }

        if (wrong == true)
        {
            // Questions the user answered incorrectly in any of their own exams.
            query = query.Where(q => db.ExamQuestions.Any(e => e.QuestionId == q.Id && e.Outcome == ExamOutcome.Incorrect && db.Exams.Any(x => x.Id == e.ExamId && x.UserId == userId)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(q => q.DocumentId).ThenBy(q => q.SourceNumber)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(q => new QuestionDto(
                q.Id,
                q.DocumentId,
                q.SourceNumber,
                q.Text,
                q.VerificationStatus,
                q.ParseWarning,
                q.Options.OrderBy(o => o.Position).Select(o => new AnswerOptionDto(o.Id, o.Label, o.Text, o.IsCorrect)).ToList(),
                q.Document.OriginalFileName,
                q.Explanation,
                db.QuestionBookmarks.Any(b => b.QuestionId == q.Id && b.UserId == userId),
                q.Document.UploadedByUserId == userId,
                Array.Empty<VisualDto>()))
            .ToListAsync(ct);

        var visuals = await QuestionVisuals.ForQuestionsAsync(db, items.Select(i => i.Id).ToList(), ct);
        items = items.Select(i => visuals.Contains(i.Id)
                ? i with { Visuals = visuals[i.Id].Select(v => QuestionVisuals.ToDto(v, i.Options.Select(o => o.Id).ToList())).ToList() }
                : i)
            .ToList();

        return TypedResults.Ok(new PagedResult<QuestionDto>(items, pageNumber, size, total));
    }

    /// <summary>
    /// Source PDFs of the user's running exam (while it still accepts answers: deadline after <paramref name="deadlineAfter"/>).
    /// allHidden: an old exam without a recorded source, which may have drawn from any PDF.
    /// </summary>
    private static async Task<(bool AllHidden, List<Guid> Hidden)> ActiveExamDocumentsAsync(
        AppDbContext db, Guid userId, DateTimeOffset deadlineAfter, CancellationToken ct)
    {
        var sources = await db.Exams.AsNoTracking()
            .Where(e => e.UserId == userId && e.Status == ExamStatus.InProgress && e.ExpiresAt >= deadlineAfter)
            .Select(e => e.SourceDocumentIds).ToListAsync(ct);
        return (sources.Any(ids => ids.Count == 0), sources.SelectMany(ids => ids).Distinct().ToList());
    }

    private static async Task<IResult> Stats(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var questions = Readable(db, userId).AsNoTracking();
        return TypedResults.Ok(new QuestionStatsDto(
            await questions.CountAsync(ct),
            await questions.Select(q => q.DocumentId).Distinct().CountAsync(ct),
            await db.QuestionBookmarks.CountAsync(b => b.UserId == userId && questions.Any(q => q.Id == b.QuestionId), ct)));
    }

    // Only the uploader may change questions; shared documents stay read-only for everyone else (404, not 403).
    private static Task<Question?> Editable(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        return db.Questions.Include(q => q.Options).FirstOrDefaultAsync(q => q.Id == id && q.Document.UploadedByUserId == userId, ct);
    }

    private static string? Invalid(Question question) => question.Options.Count == 0
        ? string.IsNullOrWhiteSpace(question.Explanation) ? "Açıq sualın cavabı boş ola bilməz." : null
        : question.Options.Count(o => o.IsCorrect) != 1 ? "Tam olaraq bir düzgün cavab seçin."
        : question.Options.Any(o => string.IsNullOrWhiteSpace(o.Text)) ? "Variant mətni boş ola bilməz." : null;

    private static async Task<IResult> Update(Guid id, UpdateQuestionRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var question = await Editable(id, user, db, ct);
        if (question is null) return TypedResults.Problem("Sual tapılmadı.", statusCode: 404);
        // Option rows keep their ids so existing exam answers stay linked; only text and the correct flag change.
        if (question.Options.Count != (request.Options?.Count ?? 0))
            return TypedResults.Problem("Variantların sayı dəyişdirilə bilməz.", statusCode: 400);
        question.Text = request.Text.Trim();
        if (question.Options.Count == 0) question.Explanation = request.Answer?.Trim();
        foreach (var option in question.Options)
        {
            var edit = request.Options!.FirstOrDefault(o => o.Id == option.Id);
            if (edit is null) return TypedResults.Problem("Variant tapılmadı.", statusCode: 400);
            option.Text = edit.Text.Trim();
            option.IsCorrect = edit.IsCorrect;
        }
        if (Invalid(question) is { } error) return TypedResults.Problem(error, statusCode: 400);
        question.VerificationStatus = VerificationStatus.Verified;
        question.ParseWarning = null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Verify(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var question = await Editable(id, user, db, ct);
        if (question is null) return TypedResults.Problem("Sual tapılmadı.", statusCode: 404);
        if (Invalid(question) is { } error) return TypedResults.Problem($"{error} Əvvəlcə sualı redaktə edin.", statusCode: 400);
        question.VerificationStatus = VerificationStatus.Verified;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Bookmark(Guid id, bool add, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (!await Readable(db, userId).AnyAsync(q => q.Id == id, ct)) return TypedResults.Problem("Sual tapılmadı.", statusCode: 404);
        if (add)
        {
            if (await db.QuestionBookmarks.AnyAsync(b => b.QuestionId == id && b.UserId == userId, ct)) return TypedResults.NoContent();
            db.QuestionBookmarks.Add(new QuestionBookmark { QuestionId = id, UserId = userId });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* a concurrent request already saved the same bookmark */ }
        }
        else
        {
            await db.QuestionBookmarks.Where(b => b.QuestionId == id && b.UserId == userId).ExecuteDeleteAsync(ct);
        }
        return TypedResults.NoContent();
    }
}

public sealed record AnswerOptionDto(Guid Id, string Label, string Text, bool IsCorrect);

public sealed record QuestionDto(
    Guid Id,
    Guid DocumentId,
    int SourceNumber,
    string Text,
    VerificationStatus VerificationStatus,
    string? ParseWarning,
    IReadOnlyList<AnswerOptionDto> Options,
    string DocumentName,
    string? Answer,
    bool IsBookmarked,
    bool CanEdit,
    IReadOnlyList<VisualDto> Visuals);

public sealed record QuestionStatsDto(int TotalQuestions, int DocumentCount, int BookmarkCount);

public sealed record UpdateOptionRequest(Guid Id, [property: Required(ErrorMessage = "Variant mətnini daxil edin."), StringLength(1000, ErrorMessage = "Variant ən çox 1000 simvol ola bilər.")] string Text, bool IsCorrect);

public sealed record UpdateQuestionRequest(
    [property: Required(ErrorMessage = "Sual mətnini daxil edin."), StringLength(4000, ErrorMessage = "Sual ən çox 4000 simvol ola bilər.")] string Text,
    string? Answer,
    List<UpdateOptionRequest>? Options);

/// <summary>A user's saved question; private to that user.</summary>
public sealed class QuestionBookmark
{
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
}

public sealed class QuestionBookmarkConfiguration : IEntityTypeConfiguration<QuestionBookmark>
{
    public void Configure(EntityTypeBuilder<QuestionBookmark> builder)
    {
        builder.ToTable("QuestionBookmarks");
        builder.HasKey(b => new { b.UserId, b.QuestionId });
        builder.HasIndex(b => b.QuestionId);
        builder.HasOne<Question>().WithMany().HasForeignKey(b => b.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
