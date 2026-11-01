using System.Security.Claims;
using System.Text.Json;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Study;

public static partial class StudyEndpoints
{
    private const string PracticeModel = "study-practice-v1";

    private static void MapPracticeEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/practice", StartPractice).Validate<StartPracticeRequest>();
        group.MapGet("/{documentId:guid}/practice/history", PracticeHistory);
        group.MapGet("/practice/{id:guid}", GetPractice);
        group.MapDelete("/practice/{id:guid}", DeletePractice);
        group.MapPost("/practice/{id:guid}/action", PracticeAction).Validate<PracticeActionRequest>();
    }

    private static async Task<IResult> StartPractice(StartPracticeRequest request, ClaimsPrincipal user, AppDbContext db,
        TimeProvider clock, CancellationToken ct)
    {
        try
        {
            if (request.Mode is not ("open" or "mcq"))
                return TypedResults.Problem("Öyrənmə parametrləri düzgün deyil.", statusCode: 400);
            var userId = user.GetUserId();
            await CheckPracticeAccess(db, request.DocumentId, userId, ct);
            var sources = await SourcesAsync(db, request.DocumentId, ct);
            var eligible = request.Mode == "mcq"
                ? sources.Where(s => StudyEngine.Options(s, sources) is not null).ToList()
                : sources;
            if (eligible.Count == 0)
                return TypedResults.Problem("Qısa və etibarlı dörd variantı olan sual tapılmadı. Başqa sənəd seçin.", statusCode: 400);
            if (eligible.Count < request.Count)
                return TypedResults.Problem($"Bu sənəddə {eligible.Count} uyğun sual var. Sual sayını {eligible.Count} və ya daha az seçin.", statusCode: 400);
            var state = new PracticeState
            {
                Mode = request.Mode, Difficulty = "none",
                QuestionIds = eligible.OrderBy(_ => Random.Shared.Next()).Take(request.Count).Select(s => s.Id).ToArray(),
            };
            var row = new StudyAiTurn
            {
                Id = Guid.NewGuid(), UserId = userId, DocumentId = request.DocumentId,
                Model = PracticeModel, Status = "Local", Message = "Sənəd əsaslı məşq", CreatedAt = clock.GetUtcNow(),
            };
            await PreparePracticeQuestion(db, clock, row, state, ct);
            await CheckPracticeAccess(db, request.DocumentId, userId, ct);
            row.ReplyJson = JsonSerializer.Serialize(state, JsonOptions);
            db.StudyAiTurns.Add(row);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(await PracticeViewAsync(db, row, state, ct));
        }
        catch (StudyAiException ex) { return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    private static async Task CheckPracticeAccess(AppDbContext db, Guid documentId, Guid userId, CancellationToken ct)
    {
        if (!await DocumentExists(db, documentId, userId, ct))
            throw new StudyAiException("Sənəd tapılmadı.", 404);
        if (await InActiveExam(db, userId, documentId, ct))
            throw new StudyAiException("Bu sənəd üzrə imtahanı tamamladıqdan sonra öyrənə bilərsiniz.", 409);
    }

    private static async Task<StudyAiTurn> LoadPractice(AppDbContext db, Guid id, Guid userId, CancellationToken ct)
    {
        var row = await db.StudyAiTurns.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && t.Model == PracticeModel && t.Status == "Local", ct);
        if (row is null) throw new StudyAiException("Məşq tapılmadı.", 404);
        await CheckPracticeAccess(db, row.DocumentId, userId, ct);
        return row;
    }

    private static async Task<IResult> DeletePractice(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var row = await db.StudyAiTurns.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId
            && t.Model == PracticeModel && t.Status == "Local", ct);
        if (row is null) return TypedResults.Problem("Məşq tapılmadı.", statusCode: 404);
        var state = JsonSerializer.Deserialize<PracticeState>(row.ReplyJson!, JsonOptions)!;
        var ids = state.AttemptIds.Append(state.AttemptId ?? Guid.Empty).Distinct().ToArray();
        var removed = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var deleted = await db.StudyAiTurns.Where(t => t.Id == id && t.UserId == userId && t.ReplyJson == row.ReplyJson)
                .ExecuteDeleteAsync(ct);
            if (deleted != 1) return false;
            await db.StudyAttempts.Where(a => a.UserId == userId && ids.Contains(a.Id)).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
        return removed ? TypedResults.NoContent() : TypedResults.Problem("Məşq dəyişib. Siyahını yeniləyin.", statusCode: 409);
    }

    private static async Task<IResult> GetPractice(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        try
        {
            var row = await LoadPractice(db, id, user.GetUserId(), ct);
            return TypedResults.Ok(await PracticeViewAsync(db, row, JsonSerializer.Deserialize<PracticeState>(row.ReplyJson!, JsonOptions)!, ct));
        }
        catch (StudyAiException ex) { return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    private static async Task<IResult> PracticeHistory(Guid documentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        try
        {
            var userId = user.GetUserId();
            await CheckPracticeAccess(db, documentId, userId, ct);
            var rows = await db.StudyAiTurns.AsNoTracking()
                .Where(t => t.UserId == userId && t.DocumentId == documentId && t.Model == PracticeModel && t.Status == "Local")
                .OrderByDescending(t => t.CreatedAt).Take(20).Select(t => new { t.Id, t.CreatedAt, t.ReplyJson }).ToListAsync(ct);
            return TypedResults.Ok(rows.Select(row =>
            {
                var state = JsonSerializer.Deserialize<PracticeState>(row.ReplyJson!, JsonOptions)!;
                var finished = state.Index >= state.QuestionIds.Length;
                return new PracticeSummary(row.Id, state.Mode, state.Difficulty, row.CreatedAt, finished,
                    state.Answers.Count, state.QuestionIds.Length, finished && state.Mode == "mcq" ? PracticeResultFor(state) : null);
            }));
        }
        catch (StudyAiException ex) { return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    private static async Task PreparePracticeQuestion(AppDbContext db, TimeProvider clock,
        StudyAiTurn row, PracticeState state, CancellationToken ct)
    {
        if (state.Index >= state.QuestionIds.Length) return;
        var sources = await SourcesAsync(db, row.DocumentId, ct);
        var source = sources.FirstOrDefault(s => s.Id == state.QuestionIds[state.Index])
            ?? throw new StudyAiException("Sual artıq mövcud deyil. Yeni məşq başladın.", 404);
        var choices = state.Mode == "mcq" ? StudyEngine.Options(source, sources) : null;
        if (state.Mode == "mcq" && choices is null)
            throw new StudyAiException("Sənəd dəyişib. Etibarlı variantlar üçün yeni məşq başladın.", 409);
        var attempt = new StudyAttempt
        {
            Id = Guid.NewGuid(), UserId = row.UserId, QuestionId = source.Id,
            Prompt = source.Text[..Math.Min(source.Text.Length, 4000)], OptionsJson = JsonSerializer.Serialize(choices ?? []),
            CorrectIndex = choices is null ? null : Array.IndexOf(choices, StudyEngine.ShortAnswer(source.Answer)),
            ExpectedAnswer = source.Answer.Length <= 4000 ? source.Answer : null, ReferenceText = StudyEngine.Topic(source),
            CreatedAt = clock.GetUtcNow(),
        };
        db.StudyAttempts.Add(attempt);
        state.AttemptId = attempt.Id;
        state.AttemptIds.Add(attempt.Id);
    }

    private static async Task<IResult> PracticeAction(Guid id, PracticeActionRequest request, ClaimsPrincipal user,
        AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        try
        {
            var row = await LoadPractice(db, id, user.GetUserId(), ct);
            var state = JsonSerializer.Deserialize<PracticeState>(row.ReplyJson!, JsonOptions)!;
            if (state.Index >= state.QuestionIds.Length || state.QuestionIds[state.Index] != request.QuestionId)
                throw new StudyAiException("Sual dəyişib. Məşqi yeniləyin.", 409);
            var attempt = await db.StudyAttempts.FirstOrDefaultAsync(a => a.Id == state.AttemptId && a.UserId == row.UserId, ct)
                ?? throw new StudyAiException("Məşq sualı tapılmadı.", 404);
            if (request.Action == "next")
            {
                if (state.Mode == "mcq" && !state.Answers.Any(a => a.QuestionId == request.QuestionId))
                    throw new StudyAiException("Əvvəlcə variant seçin.", 409);
                state.Index++;
                state.AttemptId = null;
                state.Help = null;
                state.Answer = null;
                state.Evaluation = null;
                await PreparePracticeQuestion(db, clock, row, state, ct);
                if (state.Mode == "open") attempt.CompletedAt = clock.GetUtcNow();
            }
            else if (request.Action == "respond" && state.Mode == "open")
            {
                if (string.IsNullOrWhiteSpace(request.Answer))
                    throw new StudyAiException("Cavabınızı yazın.", 400);
                attempt.Answer = request.Answer.Trim();
                state.Answer = attempt.Answer;
                var source = (await SourcesAsync(db, row.DocumentId, ct)).FirstOrDefault(s => s.Id == request.QuestionId)
                    ?? throw new StudyAiException("Sualın sənəddə cavabı tapılmadı.", 404);
                state.Evaluation = StudyEngine.Evaluate(source.Answer, attempt.Answer);
                attempt.Score = state.Evaluation.Score;
                attempt.CompletedAt = clock.GetUtcNow();
                attempt.DueAt = attempt.Score >= 80 ? clock.GetUtcNow().AddDays(3) : clock.GetUtcNow().AddMinutes(10);
            }
            else if (request.Action == "answer" && state.Mode == "mcq")
            {
                if (attempt.CompletedAt is not null) throw new StudyAiException("Bu sual artıq cavablandırılıb.", 409);
                if (request.OptionIndex is not (>= 0 and <= 3) || attempt.CorrectIndex is null)
                    throw new StudyAiException("A/B/C/D variantını seçin.", 400);
                var correct = request.OptionIndex == attempt.CorrectIndex;
                state.Answers.Add(new PracticeFeedback(attempt.QuestionId, attempt.ReferenceText ?? "Mövzu",
                    correct, attempt.ExpectedAnswer ?? (await SourcesAsync(db, row.DocumentId, ct)).FirstOrDefault(s => s.Id == attempt.QuestionId)?.Answer ?? "",
                    request.OptionIndex.Value, attempt.CorrectIndex.Value));
                attempt.Answer = ((char)('A' + request.OptionIndex.Value)).ToString();
                attempt.Score = correct ? 100 : 0;
                attempt.CompletedAt = clock.GetUtcNow();
                attempt.DueAt = correct ? clock.GetUtcNow().AddDays(3) : clock.GetUtcNow().AddMinutes(10);
            }
            else if (state.Mode == "open" && request.Action is ("hint" or "explain" or "reveal"))
            {
                var source = (await SourcesAsync(db, row.DocumentId, ct)).FirstOrDefault(s => s.Id == request.QuestionId)
                    ?? throw new StudyAiException("Sual tapılmadı.", 404);
                state.Help = request.Action switch
                {
                    "hint" => StudyEngine.Hint(source.Answer),
                    "explain" => StudyEngine.Explain(source.Answer),
                    _ => source.Answer,
                };
            }
            else throw new StudyAiException("Bu rejimdə əməliyyat mümkün deyil.", 400);
            // Recheck access before publishing document answers.
            await CheckPracticeAccess(db, row.DocumentId, row.UserId, ct);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var changed = await db.StudyAiTurns.Where(t => t.Id == row.Id && t.UserId == row.UserId && t.ReplyJson == row.ReplyJson)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplyJson, json), ct);
                if (changed != 1) throw new StudyAiException("Məşq başqa pəncərədə dəyişib. Yeniləyin.", 409);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
            return TypedResults.Ok(await PracticeViewAsync(db, row, state, ct));
        }
        catch (StudyAiException ex) { return TypedResults.Problem(ex.Message, statusCode: ex.StatusCode); }
    }

    private static PracticeResult PracticeResultFor(PracticeState state)
    {
        var correct = state.Answers.Count(a => a.Correct);
        var mistakes = state.Answers.Where(a => !a.Correct).ToArray();
        return new PracticeResult(state.Answers.Count, correct, mistakes.Length,
            state.Answers.Count == 0 ? 0 : decimal.Round(100m * correct / state.Answers.Count, 1),
            mistakes.Select(a => a.Topic).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), mistakes);
    }

    private static async Task<PracticeView> PracticeViewAsync(AppDbContext db, StudyAiTurn row, PracticeState state, CancellationToken ct)
    {
        var finished = state.Index >= state.QuestionIds.Length;
        PracticeQuestion? question = null;
        PracticeFeedback? feedback = null;
        if (!finished)
        {
            var attempt = await db.StudyAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == state.AttemptId && a.UserId == row.UserId, ct)
                ?? throw new StudyAiException("Məşq sualı tapılmadı.", 404);
            var source = await db.Questions.AsNoTracking().Where(q => q.Id == attempt.QuestionId)
                .Select(q => new { q.SourceNumber, q.Text }).FirstOrDefaultAsync(ct)
                ?? throw new StudyAiException("Sual silinib. Yeni məşq başladın.", 409);
            question = new PracticeQuestion(attempt.QuestionId, source.SourceNumber, state.Mode == "open" ? source.Text : attempt.Prompt, JsonSerializer.Deserialize<string[]>(attempt.OptionsJson)!);
            feedback = state.Answers.FirstOrDefault(a => a.QuestionId == attempt.QuestionId);
        }
        return new PracticeView(row.Id, state.Mode, state.Difficulty, finished ? state.QuestionIds.Length : state.Index + 1,
            state.QuestionIds.Length, question, feedback, state.Help, finished,
            finished && state.Mode == "mcq" ? PracticeResultFor(state) : null, state.Answer, state.Evaluation);
    }
}
