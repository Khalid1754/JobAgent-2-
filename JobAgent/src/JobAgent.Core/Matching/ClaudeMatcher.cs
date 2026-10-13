using JobAgent.Core.Claude;
using JobAgent.Core.Models;
using Microsoft.Extensions.Options;

namespace JobAgent.Core.Matching;

/// <summary>Låter Claude bedöma hur väl en annons matchar kandidatprofilen.</summary>
public sealed class ClaudeMatcher(ClaudeApiClient claude, IOptions<ClaudeOptions> options)
{
    public Task<MatchResult> MatchAsync(string candidateProfileJson, JobAd ad, CancellationToken ct)
    {
        var o = options.Value;
        var request = ClaudeApiClient.BuildToolRequest(
            o,
            MatchPrompts.System,
            candidateProfileJson,
            MatchPrompts.ToolSchemaJson,
            MatchPrompts.ToolName,
            MatchPrompts.FormatAd(ad, o.MaxAdChars) + "\nBedöm hur väl annonsen matchar kandidaten.");

        return claude.CallToolAsync<MatchResult>(request, ct);
    }
}
