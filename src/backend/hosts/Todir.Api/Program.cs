using Todir.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTodirObservability(builder.Environment.ApplicationName, builder.Environment.EnvironmentName);

var app = builder.Build();

// Keep the approved order explicit: correlation, exception handling, security headers, endpoint.
app.UseTodirCorrelation();
app.UseTodirExceptionHandling();
app.UseTodirSecurityHeaders();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
