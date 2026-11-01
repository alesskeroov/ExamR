using System.Security.Claims;
using ExamFlow.Api.Infrastructure.Data;
using ExamFlow.Api.Infrastructure.Security;
using ExamFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExamFlow.Api.Features.Auth;

public static class ProfileEndpoints
{
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    public static void MapProfileEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/profile").RequireAuthorization();
        group.MapPost("/avatar", UploadAvatar).DisableAntiforgery();
        group.MapDelete("/avatar", DeleteAvatar);
        // <img> requests cannot attach the JWT stored in localStorage, so avatar reads are public.
        api.MapGet("/profile/avatar/{userId:guid}", GetAvatar);
    }

    private static async Task<IResult> UploadAvatar(IFormFile? file, ClaimsPrincipal principal, UserManager<AppUser> users,
        IFileStorage storage, CancellationToken ct)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return TypedResults.Unauthorized();
        if (file is null || file.Length is <= 0 or > MaxAvatarBytes)
            return TypedResults.Problem("Şəkil 5 MB-dan kiçik olmalıdır.", statusCode: 400);

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var count = await input.ReadAsync(header, ct);
        var (extension, contentType) = ImageType(header.AsSpan(0, count));
        if (extension is null) return TypedResults.Problem("Yalnız PNG, JPEG və WebP şəkilləri qəbul edilir.", statusCode: 400);
        input.Position = 0;
        var name = $"avatar-{user.Id}-{Guid.NewGuid():N}.{extension}";
        await storage.SaveAsync(name, input, ct);
        var oldName = user.AvatarFileName;
        user.AvatarFileName = name;
        user.AvatarContentType = contentType;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            storage.Delete(name);
            return TypedResults.Problem("Profil şəkli yadda saxlanmadı.", statusCode: 400);
        }
        if (oldName is not null) storage.Delete(oldName);
        return TypedResults.Ok(new { avatarUrl = $"/api/profile/avatar/{user.Id}?v={name}" });
    }

    private static async Task<IResult> DeleteAvatar(ClaimsPrincipal principal, UserManager<AppUser> users, IFileStorage storage)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return TypedResults.Unauthorized();
        var oldName = user.AvatarFileName;
        user.AvatarFileName = null;
        user.AvatarContentType = null;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return TypedResults.Problem("Profil şəkli silinmədi.", statusCode: 400);
        if (oldName is not null) storage.Delete(oldName);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAvatar(Guid userId, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var avatar = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.AvatarFileName, u.AvatarContentType }).SingleOrDefaultAsync(ct);
        if (avatar?.AvatarFileName is null) return TypedResults.NotFound();
        await using var stream = storage.OpenRead(avatar.AvatarFileName);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return TypedResults.File(memory.ToArray(), avatar.AvatarContentType ?? "application/octet-stream");
    }

    private static (string? Extension, string? ContentType) ImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ("png", "image/png");
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return ("jpg", "image/jpeg");
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return ("webp", "image/webp");
        return (null, null);
    }
}
