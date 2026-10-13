using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace JobAgent.Core.Applications;

/// <summary>
/// Lägger ansökningsmejl som UTKAST i Gmail via IMAP, med CV:t bifogat.
/// Skickar aldrig något – du granskar och trycker skicka själv i Gmail.
/// Kräver ett app-lösenord (Google-konto → Säkerhet → Applösenord), sparat som GMAIL_APP_PASSWORD.
/// </summary>
public sealed class GmailDraftWriter(IOptions<ApplicationOptions> options, ILogger<GmailDraftWriter> logger)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.Value.SenderEmail) &&
        !string.IsNullOrWhiteSpace(options.Value.GmailAppPassword);

    public static bool IsValidAddress(string? address) =>
        !string.IsNullOrWhiteSpace(address) &&
        MailboxAddress.TryParse(address.Trim(), out var parsed) &&
        parsed.Address.Contains('@');

    /// <returns>Annons-ID:n för de utkast som skapades.</returns>
    public async Task<HashSet<string>> CreateDraftsAsync(IReadOnlyList<EmailDraft> drafts, CancellationToken ct)
    {
        var created = new HashSet<string>();
        if (drafts.Count == 0)
        {
            return created;
        }

        var o = options.Value;
        var cvPath = Path.GetFullPath(o.CvPath);
        var attachCv = File.Exists(cvPath);
        if (!attachCv)
        {
            logger.LogWarning("CV:t hittades inte på {Path}. Utkasten skapas utan bilaga – bifoga det själv.", cvPath);
        }

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(o.ImapHost, 993, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(o.SenderEmail, o.GmailAppPassword, ct);
        }
        catch (AuthenticationException)
        {
            logger.LogError("Gmail nekade inloggningen för {Email}. Kontrollera GMAIL_APP_PASSWORD (ett app-lösenord, inte ditt vanliga lösenord).", o.SenderEmail);
            return created;
        }

        var folder = await GetDraftsFolderAsync(client, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);

        foreach (var draft in drafts)
        {
            try
            {
                var message = BuildMessage(o, draft, attachCv ? cvPath : null);
                await folder.AppendAsync(message, MessageFlags.Draft | MessageFlags.Seen, ct);
                created.Add(draft.AdId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Kunde inte skapa utkast till {To} för annons {Id}.", draft.To, draft.AdId);
            }
        }

        await client.DisconnectAsync(true, ct);
        return created;
    }

    private static MimeMessage BuildMessage(ApplicationOptions o, EmailDraft draft, string? cvPath)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.SenderName, o.SenderEmail));
        message.To.Add(MailboxAddress.Parse(draft.To.Trim()));
        message.Subject = draft.Subject;

        var body = new BodyBuilder { TextBody = draft.Body };
        if (cvPath is not null)
        {
            body.Attachments.Add(cvPath);
        }
        message.Body = body.ToMessageBody();
        return message;
    }

    private static async Task<IMailFolder> GetDraftsFolderAsync(ImapClient client, CancellationToken ct)
    {
        // Gmail markerar utkastmappen med SPECIAL-USE, oavsett om den heter "Drafts" eller "Utkast".
        if ((client.Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) != 0)
        {
            var special = client.GetFolder(SpecialFolder.Drafts);
            if (special is not null)
            {
                return special;
            }
        }

        foreach (var name in new[] { "[Gmail]/Drafts", "[Gmail]/Utkast" })
        {
            try
            {
                return await client.GetFolderAsync(name, ct);
            }
            catch (FolderNotFoundException)
            {
                // prova nästa namn
            }
        }

        throw new InvalidOperationException("Hittade ingen utkastmapp i Gmail.");
    }
}
