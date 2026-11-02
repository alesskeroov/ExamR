using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ExamFlow.Api.Features.Learning;

public sealed record ChatModelMessage(string Role, string Content);
public sealed record ChatModelUsage(int InputTokens, int CachedInputTokens, int OutputTokens);

/// <summary>A streamed piece: text delta, or the final usage (sent last).</summary>
public sealed record ChatModelChunk(string? Delta, ChatModelUsage? Usage);

public enum ChatModelFailure { NotConfigured, RateLimited, QuotaExceeded, Unavailable, Rejected, Timeout }

/// <summary>Carries only a category; provider messages and keys never reach users or logs.</summary>
public sealed class ChatModelException(ChatModelFailure failure, int? statusCode = null) : Exception($"Chat model call failed: {failure} ({statusCode})")
{
    public ChatModelFailure Failure { get; } = failure;
}

public interface IChatModel
{
    string Model { get; }
    IAsyncEnumerable<ChatModelChunk> StreamAsync(IReadOnlyList<ChatModelMessage> messages, int maxOutputTokens, CancellationToken ct);
}

/// <summary>OpenAI Chat Completions over plain HTTP + SSE (no SDK dependency).</summary>
public sealed class OpenAiChatClient(HttpClient http, IOptions<LearningAiOptions> options, IConfiguration configuration, ILogger<OpenAiChatClient> logger) : IChatModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string Model => options.Value.Model;

    /// <summary>Environment only: OPENAI_API_KEY, or OpenAI__ApiKey (user-secrets in development map to the same key).</summary>
    public static string? ApiKey(IConfiguration configuration) =>
        configuration["OPENAI_API_KEY"] is { Length: > 0 } key ? key : configuration["OpenAI:ApiKey"] is { Length: > 0 } k ? k : null;

    public async IAsyncEnumerable<ChatModelChunk> StreamAsync(IReadOnlyList<ChatModelMessage> messages, int maxOutputTokens, [EnumeratorCancellation] CancellationToken ct)
    {
        var key = ApiKey(configuration) ?? throw new ChatModelException(ChatModelFailure.NotConfigured);
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        var body = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            stream_options = new { include_usage = true },
            max_completion_tokens = maxOutputTokens,
            temperature = 0.2,
        }, Json);

        using var response = await SendAsync(new Uri(new Uri(settings.BaseUrl), "chat/completions"), key, body, timeout.Token, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            string? line;
            try { line = await reader.ReadLineAsync(timeout.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatModelException(ChatModelFailure.Timeout); }
            if (line is null) yield break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line.AsSpan(5).Trim();
            if (data.SequenceEqual("[DONE]")) yield break;

            var chunk = Parse(data.ToString());
            if (chunk is not null) yield return chunk;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, string key, string body, CancellationToken token, CancellationToken caller)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            HttpResponseMessage response;
            try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); }
            catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw new ChatModelException(ChatModelFailure.Timeout); }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("OpenAI request failed: {Error}", ex.HttpRequestError);
                if (attempt < 2) { await Task.Delay(1000, token); continue; }
                throw new ChatModelException(ChatModelFailure.Unavailable);
            }
            if (response.IsSuccessStatusCode) return response;

            var status = (int)response.StatusCode;
            var code = await ErrorCode(response, token);
            response.Dispose();
            logger.LogWarning("OpenAI returned {Status} ({Code}) on attempt {Attempt}", status, code, attempt);
            // Only transient failures before any output are retried, so a reply is never billed twice.
            var transient = response.StatusCode is HttpStatusCode.TooManyRequests && code != "insufficient_quota" || status >= 500;
            if (transient && attempt < 2) { await Task.Delay(1500, token); continue; }
            throw new ChatModelException(status switch
            {
                401 or 403 => ChatModelFailure.NotConfigured,
                404 => ChatModelFailure.NotConfigured, // unknown model name
                429 when code == "insufficient_quota" => ChatModelFailure.QuotaExceeded,
                429 => ChatModelFailure.RateLimited,
                >= 500 => ChatModelFailure.Unavailable,
                _ => ChatModelFailure.Rejected,
            }, status);
        }
    }

    // Only the machine-readable error code is kept; the provider's message text is not logged or returned.
    private static async Task<string?> ErrorCode(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException) { return null; }
    }

    public static ChatModelChunk? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            var cached = usage.TryGetProperty("prompt_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object
                && details.TryGetProperty("cached_tokens", out var c) ? c.GetInt32() : 0;
            return new ChatModelChunk(null, new ChatModelUsage(usage.GetProperty("prompt_tokens").GetInt32(), cached, usage.GetProperty("completion_tokens").GetInt32()));
        }
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            return new ChatModelChunk(content.GetString(), null);
        return null;
    }
}
