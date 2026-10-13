using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobAgent.Core.Matching;
using Microsoft.Extensions.Logging;

namespace JobAgent.Core.Claude;

/// <summary>
/// Tunn klient för Claude Messages API, delad av matchning och brevskrivning.
/// - Tvingar fram ett tool-anrop så att svaret alltid är JSON enligt ett schema.
/// - Kandidatprofilen ligger i system-prompten med cache_control och cachas mellan anropen.
/// - Försöker igen vid 429/529/5xx.
/// HttpClient (bas-URL, x-api-key, anthropic-version) konfigureras i Program.cs.
/// </summary>
public sealed class ClaudeApiClient(HttpClient http, ILogger<ClaudeApiClient> logger)
{
    private const int MaxAttempts = 4;

    public static JsonObject BuildToolRequest(
        ClaudeOptions options,
        string systemPrompt,
        string candidateProfileJson,
        string toolSchemaJson,
        string toolName,
        string userContent) => new()
    {
        ["model"] = options.Model,
        ["max_tokens"] = options.MaxTokens,
        ["system"] = new JsonArray
        {
            new JsonObject { ["type"] = "text", ["text"] = systemPrompt },
            new JsonObject
            {
                ["type"] = "text",
                ["text"] = "<kandidatprofil>\n" + candidateProfileJson + "\n</kandidatprofil>",
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
            }
        },
        ["tools"] = new JsonArray { JsonNode.Parse(toolSchemaJson) },
        ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = toolName },
        ["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["content"] = userContent }
        }
    };

    public async Task<T> CallToolAsync<T>(JsonObject request, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(request, ct);
        var root = await response.Content.ReadFromJsonAsync<JsonNode>(ct)
                   ?? throw new InvalidOperationException("Tomt svar från Claude API.");

        LogUsage(root["usage"]);

        var toolUse = root["content"]?.AsArray()
                          .FirstOrDefault(block => (string?)block?["type"] == "tool_use")
                      ?? throw new InvalidOperationException("Claude svarade utan tool_use-block.");

        return toolUse["input"].Deserialize<T>(JsonDefaults.SnakeCase)
               ?? throw new InvalidOperationException("Kunde inte tolka svaret från Claude.");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(JsonObject request, CancellationToken ct)
    {
        var json = request.ToJsonString();

        for (var attempt = 1; ; attempt++)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync("v1/messages", content, ct);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var status = (int)response.StatusCode;
            var retryable = status is 429 or 529 or >= 500;
            if (!retryable || attempt >= MaxAttempts)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                response.Dispose();
                throw new HttpRequestException($"Claude API svarade {status}: {error}");
            }

            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            response.Dispose();
            logger.LogWarning("Claude API {Status}, försök {Attempt}/{Max}. Väntar {Delay:0}s.",
                status, attempt, MaxAttempts, delay.TotalSeconds);
            await Task.Delay(delay, ct);
        }
    }

    private void LogUsage(JsonNode? usage)
    {
        if (usage is null || !logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        logger.LogDebug("Tokens: in {In}, cache-läs {CacheRead}, cache-skriv {CacheWrite}, ut {Out}",
            (int?)usage["input_tokens"],
            (int?)usage["cache_read_input_tokens"],
            (int?)usage["cache_creation_input_tokens"],
            (int?)usage["output_tokens"]);
    }
}
