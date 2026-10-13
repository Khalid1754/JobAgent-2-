using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JobAgent.Core;
using JobAgent.Core.Applications;
using JobAgent.Core.JobTech;
using JobAgent.Core.Matching;
using JobAgent.Core.Models;
using JobAgent.Core.Reporting;
using JobAgent.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAgent.Runner;

/// <summary>
/// En körning av agenten:
/// 1. hämtar nya annonser från JobTech för alla sökord och kommuner,
/// 2. tar bort dubbletter, redan bedömda annonser och uppenbart seniora roller,
/// 3. låter Claude bedöma varje annons mot kandidatprofilen,
/// 4. skriver personliga brev för de bästa och lägger mejlansökningar som Gmail-utkast,
/// 5. skriver en rapport (Markdown + JSON) sorterad på matchpoäng.
/// Ingenting skickas automatiskt.
/// </summary>
public sealed class AgentRunner(
    JobTechClient jobTech,
    ClaudeMatcher matcher,
    LetterWriter letterWriter,
    GmailDraftWriter gmail,
    IOptions<AgentOptions> agentOptions,
    IOptions<JobTechOptions> jobTechOptions,
    IOptions<ApplicationOptions> applicationOptions,
    ILogger<AgentRunner> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var o = agentOptions.Value;
        var runAt = DateTime.Now;

        var profilePath = Path.GetFullPath(o.CandidateProfilePath);
        if (!File.Exists(profilePath))
        {
            logger.LogError("Hittar inte kandidatprofilen: {Path}. Kör från repots rotmapp.", profilePath);
            return 1;
        }
        var profile = await File.ReadAllTextAsync(profilePath, ct);

        var store = await SeenJobsStore.LoadAsync(Path.GetFullPath(o.SeenJobsPath), ct);

        // 1. Hämta
        var ads = await FetchAdsAsync(runAt, ct);

        // 2. Filtrera
        var newAds = ads.Where(a => !store.Contains(a.Id)).ToList();
        var excluded = newAds.Where(a => IsExcluded(a, o.ExcludeHeadlineKeywords)).ToList();
        var candidates = newAds.Except(excluded)
            .OrderByDescending(a => a.PublicationDate)
            .ToList();
        var toMatch = candidates.Take(o.MaxAdsToMatchPerRun).ToList();

        logger.LogInformation(
            "{Total} unika annonser, {New} nya, {Excluded} bortsorterade på rubrik, {ToMatch} skickas till matchning.",
            ads.Count, newAds.Count, excluded.Count, toMatch.Count);

        if (candidates.Count > toMatch.Count)
        {
            logger.LogInformation("{Left} annonser väntar till nästa körning (MaxAdsToMatchPerRun = {Max}).",
                candidates.Count - toMatch.Count, o.MaxAdsToMatchPerRun);
        }

        if (o.DryRun)
        {
            foreach (var ad in toMatch)
            {
                logger.LogInformation("[torrkörning] {Headline} – {Employer}, {Municipality}",
                    ad.Headline, ad.Employer?.Name, ad.Municipality);
            }
            logger.LogInformation("Torrkörning klar. Inget skickades till Claude och inget markerades som sett.");
            return 0;
        }

        // 3. Matcha
        var matches = await MatchAllAsync(profile, toMatch, o.MaxParallelRequests, ct);

        foreach (var m in matches)
        {
            store.MarkSeen(m.Ad, m.Result.Score);
        }
        foreach (var ad in excluded)
        {
            store.MarkSeen(ad, null, "bortsorterad på rubrik");
        }
        await store.SaveAsync(ct);

        var sorted = matches.OrderByDescending(m => m.Result.Score).ToList();
        var outputDir = Path.GetFullPath(o.OutputDirectory);
        Directory.CreateDirectory(outputDir);

        // 4. Brev och Gmail-utkast
        var applications = await PrepareApplicationsAsync(profile, sorted, outputDir, runAt, ct);

        // 5. Rapportera
        var markdownPath = await WriteReportAsync(sorted, applications, excluded.Count, runAt, outputDir, o, ct);

        var strong = sorted.Count(m => m.Result.Score >= o.MinScoreToHighlight);
        logger.LogInformation("Klart: {Strong} av {Count} annonser fick {Min}+ poäng, {Letters} brev skrivna, {Drafts} Gmail-utkast. Rapport: {Report}",
            strong, sorted.Count, o.MinScoreToHighlight, applications.Count,
            applications.Values.Count(a => a.DraftCreated), markdownPath);

        foreach (var m in sorted.Take(5))
        {
            logger.LogInformation("  {Score,3} · {Headline} – {Employer}",
                m.Result.Score, m.Ad.Headline, m.Ad.Employer?.Name);
        }

        return 0;
    }

    private async Task<List<JobAd>> FetchAdsAsync(DateTime runAt, CancellationToken ct)
    {
        var options = jobTechOptions.Value;
        var publishedAfter = runAt.AddDays(-options.PublishedWithinDays);
        var unique = new Dictionary<string, JobAd>();

        foreach (var query in options.Queries)
        {
            try
            {
                var hits = await jobTech.SearchAsync(query, publishedAfter, ct);
                var added = hits.Count(hit => unique.TryAdd(hit.Id, hit));
                logger.LogInformation("Sökord '{Query}': {Hits} träffar, {Added} nya i den här körningen.",
                    query, hits.Count, added);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Kunde inte hämta annonser för '{Query}'. Fortsätter med nästa sökord.", query);
            }
        }

        return unique.Values.ToList();
    }

    private async Task<List<JobMatch>> MatchAllAsync(
        string profile, IReadOnlyList<JobAd> ads, int maxParallel, CancellationToken ct)
    {
        var results = new ConcurrentBag<JobMatch>();
        var done = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, maxParallel),
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(ads, parallelOptions, async (ad, token) =>
        {
            try
            {
                var result = await matcher.MatchAsync(profile, ad, token);
                results.Add(new JobMatch(ad, result));
                logger.LogInformation("({Done}/{Total}) {Score,3} · {Headline}",
                    Interlocked.Increment(ref done), ads.Count, result.Score, ad.Headline);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // Fångar även HttpClient-timeouts (TaskCanceledException) utan att avbryta hela körningen.
                // Markeras inte som sedd, så den provas igen nästa körning.
                logger.LogWarning(ex, "Kunde inte matcha annons {Id} ({Headline}).", ad.Id, ad.Headline);
            }
        });

        return results.ToList();
    }

    private async Task<Dictionary<string, ApplicationDraft>> PrepareApplicationsAsync(
        string profile, IReadOnlyList<JobMatch> sorted, string outputDir, DateTime runAt, CancellationToken ct)
    {
        var a = applicationOptions.Value;
        var applications = new Dictionary<string, ApplicationDraft>();
        if (!a.Enabled)
        {
            return applications;
        }

        var eligible = sorted.Where(m => m.Result.Score >= a.MinScore).Take(a.MaxLettersPerRun).ToList();
        if (eligible.Count == 0)
        {
            logger.LogInformation("Inga annonser nådde {Min} poäng, så inga brev skrevs.", a.MinScore);
            return applications;
        }

        var lettersDir = Path.Combine(outputDir, "brev", runAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(lettersDir);
        var emailDrafts = new List<EmailDraft>();

        foreach (var match in eligible)
        {
            var ad = match.Ad;
            try
            {
                var letter = await letterWriter.WriteAsync(profile, match, ct);
                var body = letterWriter.WithSignature(letter);

                var email = ad.ApplicationDetails?.Email;
                var emailTo = GmailDraftWriter.IsValidAddress(email) ? email!.Trim() : null;
                var applyUrl = ad.ApplicationDetails?.Url ?? ad.WebpageUrl;

                var letterPath = Path.Combine(lettersDir, $"{match.Result.Score}-{Slug(ad.Headline)}-{ad.Id}.txt");
                await File.WriteAllTextAsync(letterPath, LetterFile(ad, letter, body, emailTo, applyUrl), Encoding.UTF8, ct);

                applications[ad.Id] = new ApplicationDraft(ad.Id, letterPath, emailTo, applyUrl, DraftCreated: false);
                if (emailTo is not null)
                {
                    emailDrafts.Add(new EmailDraft(ad.Id, emailTo, letter.EmailSubject, body));
                }

                logger.LogInformation("Brev skrivet: {Headline}{Mail}", ad.Headline,
                    emailTo is null ? "" : $" (mejlansökan till {emailTo})");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Kunde inte skriva brev för {Headline}.", ad.Headline);
            }
        }

        if (emailDrafts.Count > 0)
        {
            if (gmail.IsConfigured)
            {
                var created = await gmail.CreateDraftsAsync(emailDrafts, ct);
                foreach (var id in created)
                {
                    applications[id] = applications[id] with { DraftCreated = true };
                }
                logger.LogInformation("{Count} utkast lagda i Gmail. Granska och skicka dem själv från Utkast.", created.Count);
            }
            else
            {
                logger.LogInformation(
                    "{Count} annonser tar emot ansökan via mejl, men Gmail är inte konfigurerat. Breven finns som filer. " +
                    "Sätt GMAIL_APP_PASSWORD för att få dem som utkast.", emailDrafts.Count);
            }
        }

        return applications;
    }

    private static string LetterFile(JobAd ad, ApplicationLetter letter, string body, string? emailTo, string? applyUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Annons:  {ad.Headline} – {ad.Employer?.Name}, {ad.Municipality}");
        if (ad.ApplicationDeadline is DateTime deadline)
        {
            sb.AppendLine($"Sista ansökningsdag: {deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }
        sb.AppendLine(emailTo is not null ? $"Ansök via mejl: {emailTo}" : $"Ansök via: {applyUrl}");
        sb.AppendLine($"Ämnesrad: {letter.EmailSubject}");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();
        sb.Append(body);
        return sb.ToString();
    }

    private static async Task<string> WriteReportAsync(
        IReadOnlyList<JobMatch> sorted,
        IReadOnlyDictionary<string, ApplicationDraft> applications,
        int excludedCount,
        DateTime runAt,
        string outputDir,
        AgentOptions o,
        CancellationToken ct)
    {
        var stamp = runAt.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);

        var markdownPath = Path.Combine(outputDir, $"matchningar-{stamp}.md");
        await File.WriteAllTextAsync(markdownPath,
            MarkdownReport.Build(sorted, applications, o.MinScoreToHighlight, excludedCount, runAt, outputDir), ct);

        var jsonPath = Path.Combine(outputDir, $"matchningar-{stamp}.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(sorted, JsonDefaults.Pretty), ct);

        return markdownPath;
    }

    private static bool IsExcluded(JobAd ad, IEnumerable<string> keywords) =>
        keywords.Any(k => ad.Headline.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string Slug(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
            if (sb.Length >= 50)
            {
                break;
            }
        }
        return sb.ToString().Trim('-');
    }
}
