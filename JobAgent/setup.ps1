# JobAgent – installationsskript för Windows
# Kör från mappen där setup.ps1 ligger:
#   powershell -ExecutionPolicy Bypass -File .\setup.ps1
#
# Gör följande:
#  1. Kontrollerar att .NET 10 SDK finns
#  2. Kopierar projektet till C:\dev\JobAgent (utanför OneDrive)
#  3. Säkerställer att launchSettings.json finns
#  4. Kör dotnet restore och dotnet build
#  5. Startar en torrkörning (hämtar annonser, anropar inte Claude)

param(
    [string]$Destination = "C:\dev\JobAgent"
)

$ErrorActionPreference = "Stop"
$source = $PSScriptRoot

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# 1. .NET 10 SDK
Step "Kontrollerar .NET SDK"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet hittades inte. Installera .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
    exit 1
}
$sdks = dotnet --list-sdks
$sdks | ForEach-Object { Write-Host "   $_" }
if (-not ($sdks -match '^10\.')) {
    Write-Host ".NET 10 SDK saknas. Installera från https://dotnet.microsoft.com/download/dotnet/10.0 eller uppdatera Visual Studio via Visual Studio Installer, och kör skriptet igen." -ForegroundColor Red
    exit 1
}

# 2. Kopiera ut ur OneDrive
if ((Resolve-Path $source).Path.TrimEnd('\') -ieq $Destination.TrimEnd('\')) {
    Step "Projektet ligger redan i $Destination"
} else {
    Step "Kopierar projektet till $Destination"
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    robocopy $source $Destination /E /XD bin obj .vs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Host "Kopieringen misslyckades (robocopy-kod $LASTEXITCODE)." -ForegroundColor Red
        exit 1
    }
    Write-Host "   Klart. Den gamla mappen i OneDrive kan du ta bort när allt fungerar."
}

Set-Location $Destination

# 3. launchSettings.json
$launchDir = Join-Path $Destination "src\JobAgent.Runner\Properties"
$launchFile = Join-Path $launchDir "launchSettings.json"
if (-not (Test-Path $launchFile)) {
    Step "Skapar launchSettings.json"
    New-Item -ItemType Directory -Force -Path $launchDir | Out-Null
    $launch = @'
{
  "profiles": {
    "Torrkörning (utan Claude)": {
      "commandName": "Project",
      "commandLineArgs": "--Agent:DryRun=true",
      "workingDirectory": "$(ProjectDir)..\\.."
    },
    "Full körning": {
      "commandName": "Project",
      "workingDirectory": "$(ProjectDir)..\\.."
    }
  }
}
'@
    [System.IO.File]::WriteAllText($launchFile, $launch, (New-Object System.Text.UTF8Encoding($false)))
}

# 4. Restore + build
Step "Hämtar NuGet-paket (dotnet restore)"
dotnet restore JobAgent.slnx
if ($LASTEXITCODE -ne 0) { Write-Host "Restore misslyckades, se felet ovan." -ForegroundColor Red; exit 1 }

Step "Bygger (dotnet build)"
dotnet build JobAgent.slnx --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "Bygget misslyckades, se felet ovan." -ForegroundColor Red; exit 1 }

# 5. Torrkörning
Step "Startar torrkörning (inga anrop till Claude)"
dotnet run --project src\JobAgent.Runner --no-build -- --Agent:DryRun=true

Step "Klart!"
Write-Host "Öppna $Destination\JobAgent.slnx i Visual Studio."
Write-Host "För en riktig körning, lägg in din API-nyckel:"
Write-Host "   dotnet user-secrets set ANTHROPIC_API_KEY <din-nyckel> --project src\JobAgent.Runner"
