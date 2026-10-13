using System.Text.Json;
using JobAgent.Core.Models;

namespace JobAgent.Core.Storage;

/// <summary>
/// Håller reda på annonser som redan har bedömts, så att samma annons inte matchas
/// (och kostar tokens) två gånger. Sparas som JSON tills vi byter till SQL Server.
/// </summary>
public sealed class SeenJobsStore
{
    private readonly string _path;
    private readonly Dictionary<string, SeenJob> _jobs;

    private SeenJobsStore(string path, Dictionary<string, SeenJob> jobs)
    {
        _path = path;
        _jobs = jobs;
    }

    public int Count => _jobs.Count;

    public static async Task<SeenJobsStore> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return new SeenJobsStore(path, new Dictionary<string, SeenJob>());
        }

        await using var stream = File.OpenRead(path);
        var jobs = await JsonSerializer.DeserializeAsync<List<SeenJob>>(stream, JsonDefaults.Pretty, ct) ?? [];
        return new SeenJobsStore(path, jobs.ToDictionary(j => j.Id));
    }

    public bool Contains(string adId) => _jobs.ContainsKey(adId);

    public void MarkSeen(JobAd ad, int? score, string? note = null) =>
        _jobs[ad.Id] = new SeenJob(ad.Id, ad.Headline, ad.Employer?.Name, DateTime.Now, score, note);

    public async Task SaveAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, _jobs.Values.OrderByDescending(j => j.FirstSeen).ToList(),
            JsonDefaults.Pretty, ct);
    }
}

public sealed record SeenJob(string Id, string Headline, string? Employer, DateTime FirstSeen, int? Score, string? Note);
