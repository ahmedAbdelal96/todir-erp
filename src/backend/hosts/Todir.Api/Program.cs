using Todir.BuildingBlocks.Observability;
using Todir.Platform.Hosting;
using Todir.Modules.Sample;
using Todir.Modules.Directory;
using Todir.Modules.Catalog;
using Todir.Modules.FinBase;
using Npgsql;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTodirObservability(builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
builder.Services.AddTodirPlatform(builder.Configuration);
builder.Services.AddSampleModule();
builder.Services.AddDirectoryModule();
builder.Services.AddCatalogModule();
builder.Services.AddFinBaseModule();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var configuredOrigins = (builder.Configuration["TODIR_CORS_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
{
    if (configuredOrigins.Length == 0) policy.SetIsOriginAllowed(_ => false);
    else policy.WithOrigins(configuredOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
var keyDirectory = builder.Configuration["TODIR_DATA_PROTECTION_KEYS"];
var certificateText = builder.Configuration["TODIR_DATA_PROTECTION_CERTIFICATE_BASE64"];
var certificatePassword = builder.Configuration["TODIR_DATA_PROTECTION_CERTIFICATE_PASSWORD"];
if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(keyDirectory) || string.IsNullOrWhiteSpace(certificateText) || string.IsNullOrWhiteSpace(certificatePassword)))
    throw new InvalidOperationException("Persistent encrypted Data Protection keys are required outside development.");
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    Directory.CreateDirectory(keyDirectory);
    var dataProtection = builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
    if (!string.IsNullOrWhiteSpace(certificateText) && !string.IsNullOrWhiteSpace(certificatePassword))
    {
        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(certificateText), certificatePassword, X509KeyStorageFlags.EphemeralKeySet);
            dataProtection.ProtectKeysWithCertificate(certificate);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("TODIR_DATA_PROTECTION_CERTIFICATE_BASE64 is invalid.", exception);
        }
    }
}
var requestBodyLimit = long.TryParse(builder.Configuration["TODIR_REQUEST_BODY_LIMIT_BYTES"], out var configuredLimit) ? configuredLimit : 2_000_000;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestBodyLimit);
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("membership_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();

// Keep the approved order explicit: correlation, exception handling, security headers, endpoint.
app.UseTodirCorrelation();
app.UseTodirExceptionHandling();
app.UseTodirSecurityHeaders();
app.UseHttpsRedirection();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
    {
        try
        {
            await context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Type = "about:blank", Title = "CSRF validation failed", Status = StatusCodes.Status400BadRequest, Extensions = { ["code"] = "security.csrf_invalid" } });
            return;
        }
    }
    await next(context);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    var dataSource = services.GetService<NpgsqlDataSource>();
    if (dataSource is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    try
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (NpgsqlException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
if (builder.Configuration.GetValue<bool>("TODIR_METRICS_ENABLED")) app.MapPrometheusScrapingEndpoint("/internal/metrics");
app.MapTodirPlatform();
app.MapSampleModule();
app.MapDirectoryModule();
app.MapCatalogModule();
app.MapFinBaseModule();
app.Run();

public partial class Program;
