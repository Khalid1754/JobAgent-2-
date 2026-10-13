# JobAgent

En AI-agent i .NET 10 som hämtar nya jobbannonser från Platsbanken (via Arbetsförmedlingens
öppna JobTech-API), låter Claude bedöma hur väl varje annons matchar mitt CV och min portfolio,
och skriver en rapport sorterad på matchpoäng.

Agenten **skickar inga ansökningar**. Den hittar, sorterar och motiverar; jag söker själv.

## Så fungerar det

```
JobTech JobSearch API ──► filtrering ──► Claude (Messages API) ──► rapport (.md + .json)
  14 sökord × 4 kommuner   dubbletter,     poäng 0–100, uppfyllda     sorterad på poäng
                           redan sedda,    krav med bevis, saknade
                           "senior" m.m.   krav, projekt att lyfta
```

- **Hämtning:** sökord och kommuner (Göteborg, Mölndal, Partille, Kungälv) ligger i
  `appsettings.json`. Ingen API-nyckel behövs för JobTech.
- **Matchning:** varje annons skickas tillsammans med `data/candidate-profile.json` till Claude.
  Ett tvingat tool-anrop (`record_match`) gör att svaret alltid följer ett JSON-schema.
  Kandidatprofilen prompt-cachas, så den kostar bara fullt pris en gång per körning.
- **Minne:** bedömda annonser sparas i `data/seen-jobs.json` så att samma annons aldrig
  bedöms (eller kostar pengar) två gånger.

## Kom igång

Kräver [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Kör alla kommandon från repots rotmapp.

**1. Torrkörning (gratis, ingen nyckel):** hämtar och listar annonser utan att anropa Claude.

```bash
dotnet run --project src/JobAgent.Runner -- --Agent:DryRun=true
```

**2. Lägg in din Claude API-nyckel** (från console.anthropic.com):

```bash
dotnet user-secrets set ANTHROPIC_API_KEY sk-ant-... --project src/JobAgent.Runner
```

**3. Riktig körning:**

```bash
dotnet run --project src/JobAgent.Runner
```

Rapporten hamnar i `output/matchningar-<datum>.md`.

## Personliga brev och Gmail-utkast

För varje annons med minst `Applications:MinScore` poäng (standard 65) skriver Claude ett
personligt brev anpassat efter annonsen, med rätt projekt framlyft. Breven sparas i
`output/brev/<datum>/`.

Annonser som tar emot ansökan **via mejl** läggs dessutom som **utkast i Gmail** med CV:t bifogat.
Agenten skickar aldrig något – du öppnar Utkast i Gmail, läser och trycker skicka. Annonser med
eget ansökningsformulär får brevet som fil och en länk till formuläret i rapporten.

För att aktivera Gmail-utkast:

1. Slå på tvåstegsverifiering på Google-kontot (krävs för app-lösenord).
2. Skapa ett app-lösenord: Google-konto → Säkerhet → Applösenord.
3. Spara det:

```bash
dotnet user-secrets set GMAIL_APP_PASSWORD "abcd efgh ijkl mnop" --project src/JobAgent.Runner
```

Mejlkontot som används står i `Applications:SenderEmail`. Lägg CV:t som `data/Khalid_Hassan_CV.pdf`.

## Inställningar (`src/JobAgent.Runner/appsettings.json`)

| Inställning | Vad den gör |
|---|---|
| `JobTech:Queries` | Sökorden, ett per målroll |
| `JobTech:Municipalities` | Kommuner (concept-ID från JobTechs taxonomi) |
| `JobTech:PublishedWithinDays` | Hur gamla annonser som hämtas |
| `Agent:MaxAdsToMatchPerRun` | Kostnadsspärr per körning |
| `Agent:ExcludeHeadlineKeywords` | Rubriker med dessa ord skickas aldrig till Claude |
| `Agent:MinScoreToHighlight` | Gräns för vad som räknas som en bra match i rapporten |
| `Claude:Model` | `claude-sonnet-5-5` (bäst) eller `claude-haiku-5-5` (billigare) |

Allt kan också sättas från kommandoraden, t.ex. `-- --Agent:MinScoreToHighlight=70`.

## Projektstruktur

```
src/
  JobAgent.Core/        Klassbibliotek: JobTech-klient, Claude-matchning, lagring, rapport
  JobAgent.Runner/      Konsolapp som kör en hel omgång (DI, konfiguration, HttpClientFactory)
data/
  candidate-profile.json   Min profil, sammanställd från CV och portfolio
```

## Nästa steg

- [ ] SQL Server / Azure SQL i stället för `seen-jobs.json`, med status per annons (ny, intressant, sökt, avböjd)
- [ ] ASP.NET Core-API + React/Vite-frontend för att se listan och markera vad jag sökt
- [ ] Schemalagd körning varje morgon (Azure Functions timer eller GitHub Actions)
- [ ] Enhetstester för filtrering och prompt-formatering
