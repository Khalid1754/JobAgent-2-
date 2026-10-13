namespace JobAgent.Runner;

public sealed class AgentOptions
{
    public string CandidateProfilePath { get; set; } = "data/candidate-profile.json";
    public string SeenJobsPath { get; set; } = "data/seen-jobs.json";
    public string OutputDirectory { get; set; } = "output";

    /// <summary>Hämta och lista annonser utan att anropa Claude (gratis, kräver ingen nyckel).</summary>
    public bool DryRun { get; set; }

    public int MaxParallelRequests { get; set; } = 3;

    /// <summary>Kostnadsspärr: max antal annonser som skickas till Claude per körning.</summary>
    public int MaxAdsToMatchPerRun { get; set; } = 60;

    public int MinScoreToHighlight { get; set; } = 60;

    /// <summary>Annonser vars rubrik innehåller något av dessa ord skickas aldrig till Claude.</summary>
    public List<string> ExcludeHeadlineKeywords { get; set; } = [];
}
