using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExamFlow.Api.Common;
using ExamFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace ExamFlow.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/register", Register).Validate<RegisterRequest>();
        group.MapPost("/login", Login).Validate<LoginRequest>();
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(created.Errors
                .GroupBy(e => e.Code.Contains("Password") ? "password" : e.Code.Contains("Email") || e.Code.Contains("UserName") ? "email" : string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        await users.AddToRoleAsync(user, Roles.User);
        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Ok(await ToResponse(user, users));
    }

    private static async Task<IResult> Login(
        LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem("Çoxlu uğursuz cəhd. Bir neçə dəqiqə sonra yenidən yoxlayın.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        return result.Succeeded ? TypedResults.Ok(await ToResponse(user, users)) : InvalidCredentials();
    }

    private static async Task<IResult> Logout(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await ToResponse(user, users));
    }

    private static IResult InvalidCredentials() =>
        TypedResults.Problem("Email və ya şifrə yanlışdır.", statusCode: StatusCodes.Status401Unauthorized);

    private static async Task<UserResponse> ToResponse(AppUser user, UserManager<AppUser> users) =>
        new(user.Id, user.Email!, user.DisplayName, (await users.GetRolesAsync(user)).ToArray());
}

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Email, string DisplayName, string[] Roles);
