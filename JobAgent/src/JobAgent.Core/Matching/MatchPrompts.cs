using System.Globalization;
using System.Text;
using JobAgent.Core.Models;

namespace JobAgent.Core.Matching;

internal static class MatchPrompts
{
    public const string ToolName = "record_match";

    public const string System = """
        Du är en erfaren IT-rekryterare i Göteborg. Du bedömer hur väl en jobbannons passar en
        junior kandidat, utifrån kandidatprofilen nedan (som bygger på kandidatens CV och portfolio).

        Regler:
        - Var ärlig. Blås inte upp matchningen; kandidaten ska kunna lita på poängen.
        - Varje uppfyllt krav måste styrkas med något som faktiskt står i profilen: ett projektnamn,
          praktiken, utbildningen eller en certifiering. Hitta aldrig på erfarenhet.
        - Krav på flera års yrkeserfarenhet som utvecklare, seniora roller eller säkerhetsprövning
          sänker poängen tydligt. Annonser som välkomnar juniorer, nyexaminerade eller trainees höjer den.
        - Ta hänsyn till kandidatens målroller och matchingRules i profilen.
        - Praktiken som applikationstekniker räknas som relevant erfarenhet för support-,
          förvaltnings- och applikationsroller, men inte som utvecklarerfarenhet.

        Poängskala:
        - 80–100: stark match, sök direkt
        - 60–79: värd att söka
        - 40–59: en chansning, kandidaten saknar en del
        - 0–39: passar inte

        Svara alltid genom att anropa verktyget record_match. Skriv texterna på svenska.
        """;

    public static string ToolSchemaJson => """
        {
          "name": "record_match",
          "description": "Registrerar bedömningen av hur väl annonsen matchar kandidaten.",
          "input_schema": {
            "type": "object",
            "properties": {
              "score": { "type": "integer", "minimum": 0, "maximum": 100, "description": "Matchpoäng enligt poängskalan." },
              "verdict": { "type": "string", "enum": ["sök", "kanske", "hoppa över"] },
              "junior_friendly": { "type": "boolean", "description": "Rimlig för en junior utan flera års erfarenhet?" },
              "matched_requirements": {
                "type": "array",
                "items": {
                  "type": "object",
                  "properties": {
                    "requirement": { "type": "string", "description": "Kravet ur annonsen." },
                    "evidence": { "type": "string", "description": "Var i profilen det styrks, t.ex. projektnamn." }
                  },
                  "required": ["requirement", "evidence"]
                }
              },
              "missing_requirements": { "type": "array", "items": { "type": "string" } },
              "pitch_project": { "type": "string", "description": "Projekt eller erfarenhet att lyfta fram i personliga brevet." },
              "summary": { "type": "string", "description": "1–2 meningar om varför (eller varför inte) kandidaten ska söka." }
            },
            "required": ["score", "verdict", "junior_friendly", "matched_requirements", "missing_requirements", "pitch_project", "summary"]
          }
        }
        """;

    public static string FormatAd(JobAd ad, int maxChars)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<annons>");
        sb.AppendLine($"Rubrik: {ad.Headline}");
        sb.AppendLine($"Arbetsgivare: {ad.Employer?.Name ?? "okänd"}");
        sb.AppendLine($"Ort: {ad.Municipality}");
        AppendIfPresent(sb, "Yrke", ad.Occupation?.Label);
        AppendIfPresent(sb, "Anställningsform", ad.EmploymentType?.Label);
        AppendIfPresent(sb, "Omfattning", ad.WorkingHoursType?.Label);
        if (ad.ExperienceRequired is bool required)
        {
            sb.AppendLine($"Erfarenhet krävs: {(required ? "ja" : "nej")}");
        }
        if (ad.ApplicationDeadline is DateTime deadline)
        {
            sb.AppendLine($"Sista ansökningsdag: {deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        AppendLabels(sb, "Krav – kompetenser", ad.MustHave?.Skills);
        AppendLabels(sb, "Krav – erfarenhet", ad.MustHave?.WorkExperiences);
        AppendLabels(sb, "Krav – utbildning", ad.MustHave?.Education);
        AppendLabels(sb, "Krav – språk", ad.MustHave?.Languages);
        AppendLabels(sb, "Meriterande – kompetenser", ad.NiceToHave?.Skills);
        AppendLabels(sb, "Meriterande – erfarenhet", ad.NiceToHave?.WorkExperiences);

        var text = ad.Description?.Text ?? "";
        if (text.Length > maxChars)
        {
            text = text[..maxChars] + " …[avkortad]";
        }

        sb.AppendLine();
        sb.AppendLine("Annonstext:");
        sb.AppendLine(text);
        sb.AppendLine("</annons>");
        return sb.ToString();
    }

    private static void AppendIfPresent(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.AppendLine($"{label}: {value}");
        }
    }

    private static void AppendLabels(StringBuilder sb, string label, List<WeightedLabel>? items)
    {
        if (items is { Count: > 0 })
        {
            var labels = items.Select(i => i.Label).Where(l => !string.IsNullOrWhiteSpace(l));
            sb.AppendLine($"{label}: {string.Join(", ", labels)}");
        }
    }
}
