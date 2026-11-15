using System.Net;
using System.Text;
using ExamFlow.Api.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Tests;

public class GmailApiTransportTests
{
    private static readonly EmailMessage Message = new("student@example.test", "ExamR — şifrəni yenilə", "<p>Salam</p>", "Salam");

    private static EmailOptions Configured() => new()
    {
        Provider = "Gmail",
        From = "examr.app@gmail.com",
        FromName = "ExamR Support",
        Gmail = new GmailApiOptions { ClientId = "client", ClientSecret = "secret", RefreshToken = "refresh" },
    };

    private static (GmailApiTransport Transport, FakeHandler Handler) Create(EmailOptions options, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        var handler = new FakeHandler(responses);
        var transport = new GmailApiTransport(new HttpClient(handler), Options.Create(options), TimeProvider.System, NullLogger<GmailApiTransport>.Instance)
        {
            Backoff = (_, _) => Task.CompletedTask,
        };
        return (transport, handler);
    }

    private static HttpResponseMessage Token(string value = "access-1") =>
        new(HttpStatusCode.OK) { Content = new StringContent($$"""{"access_token":"{{value}}","expires_in":3599,"token_type":"Bearer"}""", Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("{}") };

    [Fact]
    public async Task Sends_with_refreshed_access_token()
    {
        var (transport, handler) = Create(Configured(), _ => Token(), _ => Status(HttpStatusCode.OK));

        Assert.True(await transport.SendAsync(Message, CancellationToken.None));

        Assert.Equal(GmailApiTransport.TokenEndpoint, handler.Requests[0].Uri);
        Assert.Contains("grant_type=refresh_token", handler.Requests[0].Body);
        Assert.Equal(GmailApiTransport.SendEndpoint, handler.Requests[1].Uri);
        Assert.Equal("Bearer access-1", handler.Requests[1].Authorization);
        Assert.Contains("\"raw\":", handler.Requests[1].Body);
    }

    [Fact]
    public async Task Reuses_cached_token_for_next_message()
    {
        var (transport, handler) = Create(Configured(), _ => Token(), _ => Status(HttpStatusCode.OK), _ => Status(HttpStatusCode.OK));

        await transport.SendAsync(Message, CancellationToken.None);
        await transport.SendAsync(Message, CancellationToken.None);

        Assert.Equal(1, handler.Requests.Count(r => r.Uri == GmailApiTransport.TokenEndpoint));
    }

    [Fact]
    public async Task Refreshes_token_once_after_401()
    {
        var (transport, handler) = Create(Configured(),
            _ => Token("old"), _ => Status(HttpStatusCode.Unauthorized), _ => Token("new"), _ => Status(HttpStatusCode.OK));

        Assert.True(await transport.SendAsync(Message, CancellationToken.None));
        Assert.Equal("Bearer new", handler.Requests[3].Authorization);
    }

    [Fact]
    public async Task Retries_transient_503_then_succeeds()
    {
        var (transport, handler) = Create(Configured(), _ => Token(), _ => Status(HttpStatusCode.ServiceUnavailable), _ => Status(HttpStatusCode.OK));

        Assert.True(await transport.SendAsync(Message, CancellationToken.None));
        Assert.Equal(2, handler.Requests.Count(r => r.Uri == GmailApiTransport.SendEndpoint));
    }

    [Fact]
    public async Task Stops_after_three_attempts_on_persistent_503()
    {
        var (transport, handler) = Create(Configured(), _ => Token(),
            _ => Status(HttpStatusCode.ServiceUnavailable), _ => Status(HttpStatusCode.ServiceUnavailable), _ => Status(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(Message, CancellationToken.None));
        Assert.Equal(3, handler.Requests.Count(r => r.Uri == GmailApiTransport.SendEndpoint));
    }

    [Fact]
    public async Task Does_not_retry_permanent_400()
    {
        var (transport, handler) = Create(Configured(), _ => Token(), _ => Status(HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(Message, CancellationToken.None));
        Assert.Equal(1, handler.Requests.Count(r => r.Uri == GmailApiTransport.SendEndpoint));
    }

    [Fact]
    public async Task Timeout_is_not_retried_to_avoid_duplicate_mail()
    {
        var (transport, handler) = Create(Configured(), _ => Token(), _ => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => transport.SendAsync(Message, CancellationToken.None));
        Assert.Equal(1, handler.Requests.Count(r => r.Uri == GmailApiTransport.SendEndpoint));
    }

    [Fact]
    public async Task Skips_without_configuration_and_sends_nothing()
    {
        var (transport, handler) = Create(new EmailOptions { Provider = "Gmail" });

        Assert.False(await transport.SendAsync(Message, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Invalid_refresh_token_fails_without_sending()
    {
        var (transport, handler) = Create(Configured(),
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(Message, CancellationToken.None));
        Assert.Contains("invalid_grant", error.Message);
        Assert.DoesNotContain("refresh", error.Message.Replace("refresh failed", ""));
        Assert.DoesNotContain(handler.Requests, r => r.Uri == GmailApiTransport.SendEndpoint);
    }

    [Fact]
    public void Mime_has_encoded_sender_subject_and_both_parts()
    {
        var raw = GmailMime.Build("examr.app@gmail.com", "ExamR Support", Message, boundary: "b1");
        var mime = Encoding.UTF8.GetString(Convert.FromBase64String(Base64UrlToBase64(raw)));

        Assert.DoesNotContain('+', raw);
        Assert.DoesNotContain('/', raw);
        Assert.Contains($"From: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes("ExamR Support"))}?= <examr.app@gmail.com>", mime);
        Assert.Contains($"Subject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(Message.Subject))}?=", mime);
        Assert.Contains("Content-Type: text/plain; charset=UTF-8", mime);
        Assert.Contains("Content-Type: text/html; charset=UTF-8", mime);
        Assert.EndsWith("--b1--\r\n", mime);
    }

    [Fact]
    public void Mime_adds_reply_to_only_when_configured()
    {
        static string Decode(string raw) => Encoding.UTF8.GetString(Convert.FromBase64String(Base64UrlToBase64(raw)));

        Assert.Contains("Reply-To: <examr.contact@gmail.com>\r\n", Decode(GmailMime.Build("a@gmail.com", "ExamR", Message, "b", "examr.contact@gmail.com")));
        Assert.DoesNotContain("Reply-To:", Decode(GmailMime.Build("a@gmail.com", "ExamR", Message, "b")));
        Assert.DoesNotContain("\r\nBcc:", Decode(GmailMime.Build("a@gmail.com", "ExamR", Message, "b", "a@example.test\r\nBcc: x@example.test")));
    }

    [Fact]
    public void Mime_blocks_header_injection()
    {
        var hostile = new EmailMessage("victim@example.test\r\nBcc: attacker@example.test", "Hi\r\nBcc: x@example.test", "<p>x</p>");
        var mime = Encoding.UTF8.GetString(Convert.FromBase64String(Base64UrlToBase64(GmailMime.Build("a@gmail.com", "ExamR", hostile, "b"))));

        Assert.DoesNotContain("\r\nBcc:", mime);
    }

    private static string Base64UrlToBase64(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;
        public List<(string Uri, string Body, string? Authorization)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString()));
            if (_next >= responses.Length) throw new InvalidOperationException("Unexpected extra request");
            return responses[_next++](request);
        }
    }
}
