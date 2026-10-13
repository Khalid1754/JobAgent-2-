using JobAgent.Core.Claude;
using JobAgent.Core.Matching;
using JobAgent.Core.Models;
using Microsoft.Extensions.Options;

namespace JobAgent.Core.Applications;

/// <summary>Låter Claude skriva ett personligt brev anpassat efter annonsen och matchningen.</summary>
public sealed class LetterWriter(
    ClaudeApiClient claude,
    IOptions<ClaudeOptions> claudeOptions,
    IOptions<ApplicationOptions> applicationOptions)
{
    public Task<ApplicationLetter> WriteAsync(string candidateProfileJson, JobMatch match, CancellationToken ct)
    {
        var o = claudeOptions.Value;
        var request = ClaudeApiClient.BuildToolRequest(
            o,
            LetterPrompts.System,
            candidateProfileJson,
            LetterPrompts.ToolSchemaJson,
            LetterPrompts.ToolName,
            LetterPrompts.FormatRequest(match, o.MaxAdChars));

        return claude.CallToolAsync<ApplicationLetter>(request, ct);
    }

    /// <summary>Brevet med avslutningsfras och kontaktuppgifter.</summary>
    public string WithSignature(ApplicationLetter letter)
    {
        var a = applicationOptions.Value;
        var closing = letter.Language == "en" ? "Best regards," : "Med vänliga hälsningar,";

        var signature = new[] { a.SenderName, a.SenderPhone, a.SenderEmail, a.PortfolioUrl }
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return letter.Letter.Trim() + "\n\n" + closing + "\n" + string.Join("\n", signature) + "\n";
    }
}
