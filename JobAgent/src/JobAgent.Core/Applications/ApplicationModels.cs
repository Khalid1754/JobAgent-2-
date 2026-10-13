namespace JobAgent.Core.Applications;

/// <summary>Claudes svar när ett personligt brev skrivs.</summary>
public sealed class ApplicationLetter
{
    /// <summary>"sv" eller "en", samma språk som annonsen.</summary>
    public string Language { get; init; } = "sv";

    public string EmailSubject { get; init; } = "";

    /// <summary>Brevtexten utan avslutning och signatur (läggs till av LetterWriter).</summary>
    public string Letter { get; init; } = "";
}

/// <summary>Ett mejl som ska läggas som utkast i Gmail.</summary>
public sealed record EmailDraft(string AdId, string To, string Subject, string Body);

/// <summary>Vad som gjordes för en annons: var brevet ligger och hur man ansöker.</summary>
public sealed record ApplicationDraft(string AdId, string LetterPath, string? EmailTo, string? ApplyUrl, bool DraftCreated);
