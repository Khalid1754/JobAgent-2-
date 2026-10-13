using System.Text;
using JobAgent.Core.Matching;
using JobAgent.Core.Models;

namespace JobAgent.Core.Applications;

internal static class LetterPrompts
{
    public const string ToolName = "write_application";

    public const string System = """
        Du skriver personliga brev för jobbansökningar åt kandidaten i profilen nedan.
        Kandidaten läser och skickar varje brev själv, så det ska gå att skicka utan ändringar.

        Regler:
        - Skriv på samma språk som annonsen (svenska om annonsen är på svenska, annars engelska).
        - 180–280 ord. Vanlig text, inga rubriker, ingen markdown, inga punktlistor.
        - Börja med "Hej!" (eller "Hej <namn>," om annonsen nämner en kontaktperson vid namn).
        - Inled med varför just den här rollen och arbetsgivaren, utifrån något konkret i annonsen.
        - Koppla 2–3 av annonsens viktigaste krav till konkreta bevis i profilen: projektnamn,
          praktiken som applikationstekniker, utbildningen eller certifieringarna.
        - Var ärlig om att kandidaten är junior, utan att be om ursäkt. Visa vilja och förmåga att lära.
        - Hitta aldrig på erfarenhet, siffror, teknik eller fakta som inte står i profilen eller annonsen.
        - Inga platshållare eller hakparenteser. Inga klyschor som "driven lagspelare med glimten i ögat".
        - Avsluta med en mening om att du gärna berättar mer på en intervju.
        - Skriv INTE avslutningsfras eller namn/kontaktuppgifter – det läggs till automatiskt.
        - email_subject: kort ämnesrad, t.ex. "Ansökan: Junior IT-tekniker – Khalid Hassan".

        Svara alltid genom att anropa verktyget write_application.
        """;

    public const string ToolSchemaJson = """
        {
          "name": "write_application",
          "description": "Sparar det personliga brevet för ansökan.",
          "input_schema": {
            "type": "object",
            "properties": {
              "language": { "type": "string", "enum": ["sv", "en"] },
              "email_subject": { "type": "string" },
              "letter": { "type": "string", "description": "Brevtexten utan avslutning och signatur." }
            },
            "required": ["language", "email_subject", "letter"]
          }
        }
        """;

    public static string FormatRequest(JobMatch match, int maxAdChars)
    {
        var r = match.Result;
        var sb = new StringBuilder();
        sb.Append(MatchPrompts.FormatAd(match.Ad, maxAdChars));
        sb.AppendLine();
        sb.AppendLine("<matchning>");
        sb.AppendLine($"Poäng: {r.Score}/100");
        sb.AppendLine("Uppfyllda krav: " + string.Join("; ", r.MatchedRequirements.Select(m => $"{m.Requirement} ({m.Evidence})")));
        sb.AppendLine("Saknas: " + string.Join("; ", r.MissingRequirements));
        sb.AppendLine($"Lyft fram: {r.PitchProject}");
        sb.AppendLine("</matchning>");
        sb.AppendLine();
        sb.AppendLine("Skriv ett personligt brev för den här annonsen.");
        return sb.ToString();
    }
}
