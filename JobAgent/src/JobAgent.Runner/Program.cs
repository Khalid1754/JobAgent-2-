using System.Net.Http.Headers;
using JobAgent.Core.Applications;
using JobAgent.Core.Claude;
using JobAgent.Core.JobTech;
using JobAgent.Core.Matching;
using JobAgent.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// appsettings.json läses från build-mappen; data- och output-sökvägar är relativa till
// mappen du kör från (repots rot).
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Hemligheter (ANTHROPIC_API_KEY, GMAIL_APP_PASSWORD) läses från user-secrets eller miljövariabler.
builder.Configuration.AddUserSecrets(typeof(AgentRunner).Assembly, optional: true);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<JobTechOptions>(builder.Configuration.GetSection("JobTech"));
builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetSection("Applications"));
builder.Services.PostConfigure<ApplicationOptions>(options =>
{
    // Google visar app-lösenord med mellanslag ("abcd efgh ijkl mnop") – de ska bort.
    var password = builder.Configuration["GMAIL_APP_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(password))
    {
        options.GmailAppPassword = password.Replace(" ", "");
    }
});

builder.Services.AddHttpClient<JobTechClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<JobTechOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ClaudeApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
    var apiKey = sp.GetRequiredService<IConfiguration>()["ANTHROPIC_API_KEY"];

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add("x-api-key", apiKey.Trim());
    }
});

builder.Services.AddTransient<ClaudeMatcher>();
builder.Services.AddTransient<LetterWriter>();
builder.Services.AddTransient<GmailDraftWriter>();
builder.Services.AddTransient<AgentRunner>();

using var host = builder.Build();

var agentOptions = host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
var configuration = host.Services.GetRequiredService<IConfiguration>();
if (!agentOptions.DryRun && string.IsNullOrWhiteSpace(configuration["ANTHROPIC_API_KEY"]))
{
    Console.Error.WriteLine(
        "ANTHROPIC_API_KEY saknas. Sätt den med:\n" +
        "  dotnet user-secrets set ANTHROPIC_API_KEY \"din-nyckel\" --project src/JobAgent.Runner\n" +
        "eller kör en torrkörning utan Claude:\n" +
        "  dotnet run --project src/JobAgent.Runner -- --Agent:DryRun=true");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var runner = host.Services.GetRequiredService<AgentRunner>();
return await runner.RunAsync(cts.Token);
