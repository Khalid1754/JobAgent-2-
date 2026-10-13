namespace JobAgent.Core.JobTech;

public sealed class JobTechOptions
{
    public string BaseUrl { get; set; } = "https://jobsearch.api.jobtechdev.se/";

    /// <summary>Hämta bara annonser publicerade de senaste N dagarna.</summary>
    public int PublishedWithinDays { get; set; } = 14;

    /// <summary>Max antal annonser per sökord (API:t ger max 100 per sida).</summary>
    public int MaxAdsPerQuery { get; set; } = 100;

    public List<MunicipalityFilter> Municipalities { get; set; } = [];

    public List<string> Queries { get; set; } = [];
}

/// <summary>Kommun i JobTechs taxonomi. ConceptId slås upp via taxonomy.api.jobtechdev.se.</summary>
public sealed class MunicipalityFilter
{
    public string Name { get; set; } = "";
    public string ConceptId { get; set; } = "";
}
