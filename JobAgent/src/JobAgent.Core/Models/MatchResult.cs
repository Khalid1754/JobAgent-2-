namespace JobAgent.Core.Models;

/// <summary>Claudes bedömning av hur väl en annons matchar kandidatprofilen.</summary>
public sealed class MatchResult
{
    /// <summary>0–100.</summary>
    public int Score { get; init; }

    /// <summary>"sök", "kanske" eller "hoppa över".</summary>
    public string Verdict { get; init; } = "";

    public bool JuniorFriendly { get; init; }

    public List<MatchedRequirement> MatchedRequirements { get; init; } = [];

    public List<string> MissingRequirements { get; init; } = [];

    /// <summary>Vilket projekt/erfarenhet som är bäst att lyfta fram i ansökan.</summary>
    public string? PitchProject { get; init; }

    public string Summary { get; init; } = "";
}

public sealed class MatchedRequirement
{
    public string Requirement { get; init; } = "";

    /// <summary>Var i CV/portfolio kravet styrks, t.ex. "LiFeDenTal" eller "Praktik Estrella".</summary>
    public string Evidence { get; init; } = "";
}

public sealed record JobMatch(JobAd Ad, MatchResult Result);
