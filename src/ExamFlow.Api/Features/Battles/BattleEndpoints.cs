using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Battles;

public static class BattleEndpoints
{
    public static void MapBattleEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/battles").RequireAuthorization();
        group.MapPost("/", Create).Validate<CreateBattleRequest>();
        group.MapGet("/history", History);
    }

    /// <summary>Creates a room; the caller becomes its host. Players then join over SignalR with the code.</summary>
    private static async Task<IResult> Create(
        CreateBattleRequest request, ClaimsPrincipal user, AppDbContext db, BattleService battles, CancellationToken ct)
    {
        var userId = user.GetUserId();
        if (battles.FindActiveHostedRoom(userId) is { } existing)
        {
            return TypedResults.Problem("Artıq aktiv otağınız var.", statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = existing.Code });
        }

        if (request.RangeFrom > request.RangeTo)
        {
            return TypedResults.Problem("Başlanğıc sual nömrəsi son nömrədən böyük ola bilməz.", statusCode: StatusCodes.Status400BadRequest);
        }

        var documentId = request.DocumentId!.Value;
        var documentName = await db.Documents.Where(d => d.Id == documentId).Select(d => d.OriginalFileName).FirstOrDefaultAsync(ct);
        if (documentName is null)
        {
            return TypedResults.Problem("Seçilmiş PDF tapılmadı.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Battles only use questions with a known correct answer.
        var candidates = await ExamEndpoints.Candidates(db, documentId, request.RangeFrom, request.RangeTo, ct);
        var available = ExamEngine.CountDistinct(candidates.Where(c => c.IsGradable));
        if (available < request.QuestionCount)
        {
            return TypedResults.Problem(
                $"{request.RangeFrom}–{request.RangeTo} aralığında cavabı məlum olan cəmi {available} sual var.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?> { ["available"] = available });
        }

        var code = battles.Create(userId, new BattleSettings(
            documentId, documentName, request.RangeFrom, request.RangeTo, request.QuestionCount, request.SecondsPerQuestion));
        return TypedResults.Created($"/battle/{code}", new CreateBattleResponse(code));
    }

    /// <summary>The caller's own battle results only.</summary>
    private static async Task<IResult> History(
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct, int? page = null, int? pageSize = null)
    {
        var userId = user.GetUserId();
        var (pageNumber, size) = Paging.Normalize(page, pageSize);
        var query = db.BattleResults.AsNoTracking().Where(r => r.UserId == userId);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(r => r.PlayedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        var documentIds = rows.Select(r => r.DocumentId).Distinct().ToList();
        var names = await db.Documents.AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.OriginalFileName, ct);

        var items = rows.Select(r => new BattleHistoryDto(
            r.BattleId, r.RoomCode, r.PlayedAt,
            new ExamSourceDto(r.DocumentId, names.GetValueOrDefault(r.DocumentId), r.RangeFrom, r.RangeTo),
            r.QuestionCount, r.ParticipantCount, r.Rank, r.Score, r.CorrectCount)).ToList();

        return TypedResults.Ok(new PagedResult<BattleHistoryDto>(items, pageNumber, size, total));
    }
}
