using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Infrastructure.Email;

/// <summary>
/// OAuth client used only for sending mail (scope https://www.googleapis.com/auth/gmail.send). Kept separate from the
/// Google sign-in client (Authentication:Google), which never receives Gmail permissions.
/// </summary>
public sealed class GmailApiOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
}

/// <summary>
/// Sends mail through the Gmail API over HTTPS (port 443), so it works on hosts that block SMTP (Render Free).
/// Messages are already queued by <see cref="SmtpEmailService"/>, so a request never waits for Google.
/// </summary>
public sealed class GmailApiTransport(HttpClient http, IOptions<EmailOptions> options, TimeProvider clock, ILogger<GmailApiTransport> logger) : IEmailTransport
{
    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string SendEndpoint = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
    private const int MaxAttempts = 3;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpires;

    /// <summary>Delay between attempts; tests replace it to run instantly.</summary>
    public Func<int, CancellationToken, Task> Backoff { get; init; } = (attempt, ct) =>
        Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)), ct);

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var gmail = settings.Gmail;
        if (string.IsNullOrWhiteSpace(gmail.ClientId) || string.IsNullOrWhiteSpace(gmail.ClientSecret)
            || string.IsNullOrWhiteSpace(gmail.RefreshToken) || string.IsNullOrWhiteSpace(settings.From))
        {
            logger.LogWarning("Gmail API is not configured (Email:Gmail:ClientId/ClientSecret/RefreshToken, Email:From); email \"{Subject}\" was not sent", message.Subject);
            return false;
        }

        var raw = GmailMime.Build(settings.From!, settings.FromName ?? "ExamR Support", message);
        var refreshedAfter401 = false;
        for (var attempt = 1; ; attempt++)
        {
            var token = await AccessTokenAsync(gmail, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint) { Content = JsonContent.Create(new { raw }) };
            request.Headers.Authorization = new("Bearer", token);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                // Connection-level failure (DNS, refused, reset): Google did not answer, so a retry is safe enough.
                logger.LogWarning("Gmail API attempt {Attempt} for \"{Subject}\" failed ({Error}); retrying", attempt, message.Subject, ex.HttpRequestError);
                await Backoff(attempt, ct);
                continue;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout: not retried, because Google may already have accepted the message and a second send would
                // deliver it twice. Rethrown as TimeoutException so the queue logs it instead of treating it as shutdown.
                throw new TimeoutException($"Gmail API did not answer in time for \"{message.Subject}\"; not retried to avoid a duplicate.");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode) return true;
                var status = (int)response.StatusCode;
                if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedAfter401)
                {
                    // Cached access token was revoked or expired early: fetch a new one once.
                    refreshedAfter401 = true;
                    InvalidateToken();
                    attempt--;
                    continue;
                }
                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    logger.LogWarning("Gmail API attempt {Attempt} for \"{Subject}\" returned {Status}; retrying", attempt, message.Subject, status);
                    await Backoff(attempt, ct);
                    continue;
                }
                throw new InvalidOperationException($"Gmail API responded {status}: {await ErrorReason(response, ct)}");
            }
        }
    }

    public static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private void InvalidateToken()
    {
        _accessToken = null;
        _accessTokenExpires = DateTimeOffset.MinValue;
    }

    /// <summary>Short-lived access token from the long-lived refresh token, cached until a minute before expiry.</summary>
    private async Task<string> AccessTokenAsync(GmailApiOptions gmail, CancellationToken ct)
    {
        if (_accessToken is not null && clock.GetUtcNow() < _accessTokenExpires) return _accessToken;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && clock.GetUtcNow() < _accessTokenExpires) return _accessToken;
            for (var attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["client_id"] = gmail.ClientId!,
                        ["client_secret"] = gmail.ClientSecret!,
                        ["refresh_token"] = gmail.RefreshToken!,
                    }),
                };
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                        ?? throw new InvalidOperationException("Google token endpoint returned an empty body.");
                    _accessToken = token.AccessToken;
                    _accessTokenExpires = clock.GetUtcNow().AddSeconds(Math.Max(60, token.ExpiresIn) - 60);
                    return _accessToken;
                }
                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    await Backoff(attempt, ct);
                    continue;
                }
                // invalid_grant = refresh token revoked/expired (e.g. consent screen left in "Testing" for 7 days).
                throw new InvalidOperationException($"Google token refresh failed ({(int)response.StatusCode}): {await ErrorReason(response, ct)}");
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>Google's short error code/message only; bodies never contain our tokens, and they are trimmed anyway.</summary>
    private static async Task<string> ErrorReason(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return body.Length > 300 ? body[..300] : body;
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>RFC 5322 / MIME message for the Gmail API "raw" field (base64url), plain text + HTML alternatives.</summary>
public static class GmailMime
{
    public static string Build(string fromAddress, string fromName, EmailMessage message, string? boundary = null)
    {
        // Header injection guard: addresses and subject never carry line breaks into the header block.
        static string Clean(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();
        static string Encoded(string value) => $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(Clean(value)))}?=";
        static string Body(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value), Base64FormattingOptions.InsertLineBreaks);

        boundary ??= $"examr-{Guid.NewGuid():N}";
        var mime = new StringBuilder()
            .Append($"From: {Encoded(fromName)} <{Clean(fromAddress)}>\r\n")
            .Append($"To: <{Clean(message.To)}>\r\n")
            .Append($"Subject: {Encoded(message.Subject)}\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n\r\n");
        if (message.TextBody is not null)
            mime.Append($"--{boundary}\r\nContent-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: base64\r\n\r\n{Body(message.TextBody)}\r\n");
        mime.Append($"--{boundary}\r\nContent-Type: text/html; charset=UTF-8\r\nContent-Transfer-Encoding: base64\r\n\r\n{Body(message.HtmlBody)}\r\n")
            .Append($"--{boundary}--\r\n");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(mime.ToString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
