namespace JobAgent.Core.Models;

// Motsvarar (en del av) svaret från JobTech JobSearch API, GET /search.
// Fältnamnen mappas från snake_case via JsonDefaults.SnakeCase.

public sealed class SearchResponse
{
    public SearchTotal? Total { get; init; }
    public List<JobAd> Hits { get; init; } = [];
}

public sealed class SearchTotal
{
    public int Value { get; init; }
}

public sealed class JobAd
{
    public string Id { get; init; } = "";
    public string Headline { get; init; } = "";
    public string? WebpageUrl { get; init; }
    public DateTime? PublicationDate { get; init; }
    public DateTime? ApplicationDeadline { get; init; }
    public bool? ExperienceRequired { get; init; }

    public Employer? Employer { get; init; }
    public Description? Description { get; init; }
    public ConceptLabel? Occupation { get; init; }
    public ConceptLabel? EmploymentType { get; init; }
    public ConceptLabel? WorkingHoursType { get; init; }
    public WorkplaceAddress? WorkplaceAddress { get; init; }
    public ApplicationDetails? ApplicationDetails { get; init; }

    public Requirements? MustHave { get; init; }
    public Requirements? NiceToHave { get; init; }

    public string Municipality => WorkplaceAddress?.Municipality ?? "okänd ort";
}

public sealed class Employer
{
    public string? Name { get; init; }
    public string? Workplace { get; init; }
}

public sealed class Description
{
    public string? Text { get; init; }
}

public sealed class ConceptLabel
{
    public string? ConceptId { get; init; }
    public string? Label { get; init; }
}

public sealed class WorkplaceAddress
{
    public string? Municipality { get; init; }
    public string? MunicipalityConceptId { get; init; }
    public string? City { get; init; }
}

public sealed class ApplicationDetails
{
    public string? Email { get; init; }
    public string? Url { get; init; }
    public bool? ViaAf { get; init; }
}

public sealed class Requirements
{
    public List<WeightedLabel> Skills { get; init; } = [];
    public List<WeightedLabel> Languages { get; init; } = [];
    public List<WeightedLabel> WorkExperiences { get; init; } = [];
    public List<WeightedLabel> Education { get; init; } = [];
}

public sealed class WeightedLabel
{
    public string? Label { get; init; }
    public int? Weight { get; init; }
}
