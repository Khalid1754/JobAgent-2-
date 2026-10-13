using System.Globalization;
using System.Text;
using JobAgent.Core.Applications;
using JobAgent.Core.Models;

namespace JobAgent.Core.Reporting;

public static class MarkdownReport
{
    public static string Build(
        IReadOnlyList<JobMatch> matches,
        IReadOnlyDictionary<string, ApplicationDraft> applications,
        int minScoreToHighlight,
        int excludedCount,
        DateTime runAt,
        string outputDir)
    {
        var top = matches.Where(m => m.Result.Score >= minScoreToHighlight).ToList();
        var rest = matches.Where(m => m.Result.Score < minScoreToHighlight).ToList();
        var drafts = applications.Values.Count(a => a.DraftCreated);

        var sb = new StringBuilder();
        sb.AppendLine($"# Jobbmatchningar {runAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine($"{matches.Count} nya annonser bedömda, {top.Count} med poäng {minScoreToHighlight} eller högre. " +
                      $"{excludedCount} sorterades bort på rubriken (t.ex. senior).");
        if (applications.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{applications.Count} personliga brev skrivna. {drafts} mejlansökningar ligger som **utkast i Gmail** – " +
                          "granska och skicka dem själv. Inget har skickats.");
        }
        sb.AppendLine();

        if (top.Count == 0)
        {
            sb.AppendLine("_Inga starka matchningar i den här körningen._");
            sb.AppendLine();
        }

        var rank = 1;
        foreach (var (ad, result) in top)
        {
            sb.AppendLine($"## {rank++}. {ad.Headline}");
            sb.AppendLine();
            sb.AppendLine($"**{result.Score}/100 · {result.Verdict}**" +
                          (result.JuniorFriendly ? " · juniorvänlig" : "") +
                          $" · {ad.Employer?.Name ?? "okänd arbetsgivare"}, {ad.Municipality}");
            sb.AppendLine();
            sb.AppendLine(result.Summary);
            sb.AppendLine();

            if (result.MatchedRequirements.Count > 0)
            {
                sb.AppendLine("**Du uppfyller:**");
                foreach (var m in result.MatchedRequirements)
                {
                    sb.AppendLine($"- {m.Requirement} ({m.Evidence})");
                }
                sb.AppendLine();
            }

            if (result.MissingRequirements.Count > 0)
            {
                sb.AppendLine("**Saknas:** " + string.Join("; ", result.MissingRequirements));
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(result.PitchProject))
            {
                sb.AppendLine($"**Lyft fram i brevet:** {result.PitchProject}");
                sb.AppendLine();
            }

            if (applications.TryGetValue(ad.Id, out var app))
            {
                if (app.DraftCreated)
                {
                    sb.AppendLine($"**Gmail-utkast skapat** till {app.EmailTo}, med CV bifogat.  ");
                }
                else if (app.EmailTo is not null)
                {
                    sb.AppendLine($"**Ansök via mejl:** {app.EmailTo}  ");
                }
                else if (!string.IsNullOrWhiteSpace(app.ApplyUrl))
                {
                    sb.AppendLine($"**Ansök via:** [arbetsgivarens formulär]({app.ApplyUrl})  ");
                }
                sb.AppendLine($"**Personligt brev:** `{Path.GetRelativePath(outputDir, app.LetterPath)}`");
                sb.AppendLine();
            }

            if (ad.ApplicationDeadline is DateTime deadline)
            {
                sb.AppendLine($"Sista ansökningsdag: {deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}  ");
            }
            if (!string.IsNullOrWhiteSpace(ad.WebpageUrl))
            {
                sb.AppendLine($"[Öppna annonsen]({ad.WebpageUrl})");
            }
            sb.AppendLine();
        }

        if (rest.Count > 0)
        {
            sb.AppendLine($"## Under {minScoreToHighlight} poäng");
            sb.AppendLine();
            foreach (var (ad, result) in rest)
            {
                var link = string.IsNullOrWhiteSpace(ad.WebpageUrl) ? ad.Headline : $"[{ad.Headline}]({ad.WebpageUrl})";
                sb.AppendLine($"- {result.Score} · {link} – {ad.Employer?.Name}. {result.Summary}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
