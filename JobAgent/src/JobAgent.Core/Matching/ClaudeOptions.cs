namespace JobAgent.Core.Matching;

public sealed class ClaudeOptions
{
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    /// <summary>claude-sonnet-5-5 ger bäst bedömningar; claude-haiku-5-5 är billigare.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";

    public int MaxTokens { get; set; } = 1500;

    /// <summary>Långa annonser kortas av för att hålla nere kostnaden.</summary>
    public int MaxAdChars { get; set; } = 12000;
}
