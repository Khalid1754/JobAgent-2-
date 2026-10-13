using System.Globalization;
using System.Net.Http.Json;
using JobAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAgent.Core.JobTech;

/// <summary>
/// Klient för Arbetsförmedlingens öppna JobSearch API (samma annonser som Platsbanken).
/// Kräver ingen API-nyckel. Dokumentation: https://jobsearch.api.jobtechdev.se/
/// </summary>
public sealed class JobTechClient(HttpClient http, IOptions<JobTechOptions> options, ILogger<JobTechClient> logger)
{
    private const int PageSize = 100; // API:ts maxgräns per anrop

    public async Task<IReadOnlyList<JobAd>> SearchAsync(string query, DateTime publishedAfter, CancellationToken ct)
    {
        var o = options.Value;
        var ads = new List<JobAd>();

        for (var offset = 0; offset < o.MaxAdsPerQuery; offset += PageSize)
        {
            var limit = Math.Min(PageSize, o.MaxAdsPerQuery - offset);
            var url = BuildUrl(query, publishedAfter, offset, limit, o.Municipalities);

            var page = await http.GetFromJsonAsync<SearchResponse>(url, JsonDefaults.SnakeCase, ct);
            if (page is null || page.Hits.Count == 0)
            {
                break;
            }

            ads.AddRange(page.Hits);
            logger.LogDebug("JobTech '{Query}' offset {Offset}: {Count} av {Total}",
                query, offset, page.Hits.Count, page.Total?.Value);

            if (ads.Count >= (page.Total?.Value ?? 0))
            {
                break;
            }
        }

        return ads;
    }

    private static string BuildUrl(
        string query, DateTime publishedAfter, int offset, int limit, IEnumerable<MunicipalityFilter> municipalities)
    {
        var parameters = new List<string>
        {
            "q=" + Uri.EscapeDataString(query),
            "published-after=" + Uri.EscapeDataString(publishedAfter.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
            "offset=" + offset,
            "limit=" + limit
        };

        // Flera kommuner = upprepad parameter, API:t tolkar det som "någon av".
        parameters.AddRange(municipalities.Select(m => "municipality=" + Uri.EscapeDataString(m.ConceptId)));

        return "search?" + string.Join('&', parameters);
    }
}
