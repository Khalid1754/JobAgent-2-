namespace JobAgent.Core.Applications;

public sealed class ApplicationOptions
{
    /// <summary>Skriv personliga brev (och Gmail-utkast) för de bästa matchningarna.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Lägsta matchpoäng för att få ett brev.</summary>
    public int MinScore { get; set; } = 65;

    /// <summary>Kostnadsspärr: max antal brev per körning.</summary>
    public int MaxLettersPerRun { get; set; } = 15;

    public string CvPath { get; set; } = "data/Khalid_Hassan_CV.pdf";

    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SenderPhone { get; set; } = "";
    public string PortfolioUrl { get; set; } = "";

    public string ImapHost { get; set; } = "imap.gmail.com";

    /// <summary>Sätts från secret GMAIL_APP_PASSWORD, aldrig i appsettings.json.</summary>
    public string? GmailAppPassword { get; set; }
}
