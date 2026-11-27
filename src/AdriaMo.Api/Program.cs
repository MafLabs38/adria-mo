using AdriaMo.Api;

var builder = WebApplication.CreateBuilder(args);
// Settings come from appsettings.json, then from the environment with the ADRIAMO__ prefix (ADRIAMO__SECTION__KEY).
builder.Configuration.AddEnvironmentVariables(prefix: "ADRIAMO__");
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health/live");
app.MapGroup("/api/v1").MapInfo();

await app.RunAsync();

/// <summary>The entry point, visible to the integration tests (WebApplicationFactory).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "WebApplicationFactory<Program> needs a public type to start the host; it is not a utility class.")]
public partial class Program;
