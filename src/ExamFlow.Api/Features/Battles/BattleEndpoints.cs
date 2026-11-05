using ExamFlow.Api.Features.Documents;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using ExamFlow.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace ExamFlow.Api.Features.Battles;

public static class BattleEndpoints
{
    public static void MapBattleEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/battles").RequireAuthorization();
        group.MapPost("/", Create).Validate<CreateBattleRequest>();
        group.MapGet("/history", History);
        group.MapGet("/rooms", MyRooms);
        group.MapPost("/rooms/{code}/close", CloseRoom);
        group.MapGet("/rooms/{code}/visuals/{visualId:guid}", Visual);
    }

    private static async Task<IResult> Visual(
        string code, Guid visualId, ClaimsPrincipal user, BattleService battles, IFileStorage storage, HttpResponse response)
    {
        var visual = await battles.FindVisualAsync(code, user.GetUserId(), visualId);
        return visual is null
            ? TypedResults.Problem("Şəkil tapılmadı.", statusCode: StatusCodes.Status404NotFound)
            : QuestionVisuals.File(storage, response, visual.StorageKey, visual.ContentType);
    }

    /// <summary>Creates a room; the caller becomes its host. Players then join over SignalR with the code.</summary>
    private static async Task<IResult> Create(
        CreateBattleRequest request, ClaimsPrincipal user, AppDbContext db, BattleService battles, CancellationToken ct)
    {
        var userId = user.GetUserId();
        var existingCode = await db.StoredBattleRooms.AsNoTracking()
            .Where(r => r.HostUserId == userId && !r.IsClosed && r.Status != RoomStatus.Completed).Select(r => r.Code).FirstOrDefaultAsync(ct);
        if (existingCode is not null)
        {
            return TypedResults.Problem("Artıq aktiv otağınız var.", statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["code"] = existingCode });
        }

        if (request.RangeFrom > request.RangeTo)
        {
            return TypedResults.Problem("Başlanğıc sual nömrəsi son nömrədən böyük ola bilməz.", statusCode: StatusCodes.Status400BadRequest);
        }

        var documentId = request.DocumentId!.Value;
        var documentName = await DocumentAccess.ForBattle(db, userId).Where(d => d.Id == documentId
            && d.Questions.Any(q => q.Options.Count >= 2)
                && !d.Questions.Any(q => q.Explanation != null && q.Explanation != "")).Select(d => d.OriginalFileName).FirstOrDefaultAsync(ct);
        if (documentName is null)
        {
            return TypedResults.Problem("Sənəd tapılmadı.", statusCode: StatusCodes.Status404NotFound);
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

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) name = "Battle otağı";
        var hostName = await db.Users.Where(u => u.Id == userId).Select(u => u.Nickname).SingleAsync(ct);
        var code = await battles.CreateAsync(userId, hostName, name, new BattleSettings(
            documentId, documentName, request.RangeFrom, request.RangeTo, request.QuestionCount, request.SecondsPerQuestion), ct);
        return TypedResults.Created($"/battle/{code}", new CreateBattleResponse(code));
    }

    private static async Task<IResult> MyRooms(bool? includeCompleted, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var id = user.GetUserId();
        var rooms = await db.StoredBattleRooms.AsNoTracking()
            .Where(r => r.CreatedById == id || r.Members.Any(m => m.UserId == id && !m.IsKicked))
            .Where(r => includeCompleted == true || r.Status != RoomStatus.Completed)
            .OrderByDescending(r => r.UpdatedAt).Take(100)
            .Select(r => new MyBattleRoomDto(r.Id, r.Name, r.Code, r.DocumentName,
                r.Members.Count(m => !m.IsKicked), r.Status, r.HostUserId == id, r.IsClosed,
                !r.IsClosed && r.Members.Any(m => m.UserId == id && !m.IsKicked))).ToListAsync(ct);
        return TypedResults.Ok(rooms);
    }

    private static async Task<IResult> CloseRoom(string code, ClaimsPrincipal user, AppDbContext db, BattleService battles, CancellationToken ct)
    {
        var id = user.GetUserId();
        if (!await db.StoredBattleRooms.AsNoTracking().AnyAsync(r => r.Code == code && r.HostUserId == id, ct)) return TypedResults.NotFound();
        try { await battles.CloseRoomAsync(code, id, ct); return TypedResults.NoContent(); }
        catch (HubException ex) { return TypedResults.Problem(ex.Message, statusCode: 409); }
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
            r.QuestionCount, r.ParticipantCount, r.Rank, r.Score, r.CorrectCount, r.RatingBefore, r.RatingAfter)).ToList();

        return TypedResults.Ok(new PagedResult<BattleHistoryDto>(items, pageNumber, size, total));
    }
}
