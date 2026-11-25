using System.Net;
using System.Security.Cryptography;
using System.Text;
using Shared.BuildingBlocks.Observability;
using Core.Hosting;
using Modules.Sample;
using Modules.Directory;
using Modules.Catalog;
using Modules.FinanceBase;
using Modules.Pricing;
using Modules.Tax;
using Modules.Ledger;
using Modules.Subledger;
using Modules.Invoicing;
using Modules.Invoicing.Controllers;
using Modules.Payments;
using Modules.Banking;
using Modules.Inventory;
using Modules.Sales;
using Modules.Purchasing;
using Modules.Accounting.Controllers;
using Modules.Hr;
using Modules.Payroll;
using Modules.CRM;
using Modules.Expenses;
using Modules.POS;
using Modules.FixedAssets.Controllers;
using Modules.Projects;
using Modules.Manufacturing;
using Modules.Service;
using Modules.Quality;
using Modules.Planning;
using Npgsql;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Validation.AspNetCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Core.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddRepositoryConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);
builder.Services.AddObservability(builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
builder.Services.AddOpenApi("v1");
builder.Services.AddCore(builder.Configuration);
builder.Services.AddSampleModule();
builder.Services.AddDirectoryModule();
builder.Services.AddCatalogModule();
builder.Services.AddFinanceBaseModule();
builder.Services.AddPricingModule();
builder.Services.AddTaxModule();
builder.Services.AddLedgerModule();
builder.Services.AddSubledgerModule();
builder.Services.AddInvoicingModule();
builder.Services.AddPaymentsModule();
builder.Services.AddBankingModule();
builder.Services.AddInventoryModule();
builder.Services.AddSalesModule();
builder.Services.AddPurchasingModule();
builder.Services.AddAccountingModule();
builder.Services.AddHrModule();
builder.Services.AddPayrollModule();
builder.Services.AddCrmModule();
builder.Services.AddExpensesModule();
builder.Services.AddPosModule();
builder.Services.AddFixedAssetsModule();
builder.Services.AddProjectsModule();
builder.Services.AddManufacturingModule();
builder.Services.AddQualityModule();
builder.Services.AddPlanningModule();
builder.Services.AddServiceModule();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var configuredOrigins = (builder.Configuration["TODIR_CORS_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
{
    if (configuredOrigins.Length == 0) policy.SetIsOriginAllowed(_ => false);
    else policy.WithOrigins(configuredOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
var trustedProxyAddresses = (builder.Configuration["TODIR_TRUSTED_PROXY_ADDRESSES"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var trustedProxyNetworks = (builder.Configuration["TODIR_TRUSTED_PROXY_NETWORKS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (trustedProxyAddresses.Length > 0 && trustedProxyNetworks.Length > 0)
    throw new InvalidOperationException("Configure either TODIR_TRUSTED_PROXY_ADDRESSES or TODIR_TRUSTED_PROXY_NETWORKS, not both.");
if (trustedProxyAddresses.Length > 0 || trustedProxyNetworks.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var address in trustedProxyAddresses)
        {
            if (!System.Net.IPAddress.TryParse(address, out var parsedAddress))
                throw new InvalidOperationException("TODIR_TRUSTED_PROXY_ADDRESSES must contain valid IP addresses.");
            options.KnownProxies.Add(parsedAddress);
        }
        foreach (var network in trustedProxyNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsedNetwork))
                throw new InvalidOperationException("TODIR_TRUSTED_PROXY_NETWORKS must contain valid CIDR networks.");
            options.KnownIPNetworks.Add(parsedNetwork);
        }
    });
}
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
    var apiPermitLimit = int.TryParse(builder.Configuration["TODIR_RATE_LIMIT_API_PERMITS"], out var customApiLimit) ? customApiLimit : 120;
    var authPermitLimit = int.TryParse(builder.Configuration["TODIR_RATE_LIMIT_AUTH_PERMITS"], out var customAuthLimit) ? customAuthLimit : 10;

    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("membership_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = apiPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (!app.Environment.IsDevelopment()) app.UseHsts();
if (trustedProxyAddresses.Length > 0 || trustedProxyNetworks.Length > 0) app.UseForwardedHeaders();

// Keep the approved order explicit: correlation, exception handling, security headers, endpoint.
app.UseCorrelation();
app.UseExceptionHandling();
app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestContextLogging();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1") && context.Request.Headers.Authorization.Count > 0)
    {
        var bearer = await context.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        if (bearer.Succeeded && !OAuthScopeAuthorization.IsAllowed(context, bearer.Principal!))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "oauth.scope_required", extensions: new Dictionary<string, object?> { ["code"] = "oauth.scope_required" }).ExecuteAsync(context);
            return;
        }
    }
    await next(context);
});
app.Use(async (context, next) =>
{
    var providerWebhook = context.Request.Path.StartsWithSegments("/api/v1/payments/webhooks/reference");
    var oauthProtocolEndpoint = context.Request.Path.StartsWithSegments("/connect");
    var mutative = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
    var cookieAuthentication = mutative ? await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme) : null;
    var bearerAuthentication = mutative && context.Request.Headers.Authorization.Count > 0
        ? await context.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        : null;
    var bearerOnly = bearerAuthentication?.Succeeded == true && cookieAuthentication?.Succeeded != true;
    if (!providerWebhook && !oauthProtocolEndpoint && !bearerOnly && mutative)
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
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("TODIR_OBSERVABILITY_TEST_FAILURE_ENABLED"))
{
    app.MapGet("/internal/observability/test-failure", (HttpContext context) =>
    {
        if (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
            return Results.NotFound();

        var expectedToken = builder.Configuration["TODIR_OBSERVABILITY_TEST_TOKEN"];
        var suppliedToken = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(expectedToken)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes($"Bearer {expectedToken}"),
                Encoding.UTF8.GetBytes(suppliedToken)))
            return Results.NotFound();

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Controlled observability test failure");
    });
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("TODIR_ENABLE_ACCEPTANCE_DIAGNOSTICS"))
{
    var expectedToken = builder.Configuration["TODIR_ACCEPTANCE_DIAGNOSTIC_TOKEN"];
    if (!string.IsNullOrWhiteSpace(expectedToken))
    {
        app.MapGet("/internal/acceptance/database-context", async (HttpContext context, IServiceProvider services, CancellationToken cancellationToken) =>
        {
            // Enforce loopback caller restriction (reject null or non-loopback)
            if (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Enforce diagnostic token authentication
            var suppliedToken = context.Request.Headers["X-Acceptance-Diagnostic-Token"].ToString();
            if (string.IsNullOrWhiteSpace(suppliedToken) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedToken), Encoding.UTF8.GetBytes(suppliedToken)))
            {
                return Results.StatusCode(StatusCodes.Status401Unauthorized);
            }

            var dataSource = services.GetService<NpgsqlDataSource>();
            if (dataSource is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await using var command = new NpgsqlCommand("SELECT current_database();", connection);
                var dbObj = await command.ExecuteScalarAsync(cancellationToken);
                var db = dbObj?.ToString() ?? string.Empty;
                var isDisposable = (db.EndsWith("_acceptance", StringComparison.OrdinalIgnoreCase) || db.Contains("_test", StringComparison.OrdinalIgnoreCase))
                    && !string.Equals(db, "todir", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(db, "todir_dev", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(db, "postgres", StringComparison.OrdinalIgnoreCase);

                return Results.Ok(new
                {
                    database = db,
                    environment = app.Environment.EnvironmentName,
                    isDisposable
                });
            }
            catch (Exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database context check unavailable");
            }
        });
    }
}
if (builder.Configuration.GetValue<bool>("TODIR_METRICS_ENABLED"))
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.Equals("/internal/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var configuredToken = builder.Configuration["TODIR_METRICS_TOKEN"];
        var suppliedToken = context.Request.Headers.Authorization.ToString();
        var authorized = !string.IsNullOrWhiteSpace(configuredToken)
            ? CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes($"Bearer {configuredToken}"),
                Encoding.UTF8.GetBytes(suppliedToken))
            : context.Connection.RemoteIpAddress is not null && IPAddress.IsLoopback(context.Connection.RemoteIpAddress);
        if (!authorized)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
    app.MapPrometheusScrapingEndpoint("/internal/metrics");
}
app.MapCore();
app.MapSampleModule();
app.MapDirectoryModule();
app.MapCatalogModule();
app.MapFinanceBaseModule();
app.MapPricingModule();
app.MapTaxModule();
app.MapSubledgerModule();
app.MapLedgerModule();
app.MapInvoicingModule();
app.MapPaymentsModule();
app.MapBankingModule();
app.MapInventoryModule();
app.MapSalesModule();
app.MapPurchasingModule();
app.MapAccountingModule();
app.MapHrModule();
app.MapPayrollModule();
app.MapCrmModule();
app.MapExpensesModule();
app.MapPosModule();
app.MapFixedAssetsModule();
app.MapProjectsModule();
app.MapManufacturingModule();
app.MapQualityModule();
app.MapPlanningModule();
app.MapServiceModule();
app.Run();

public partial class Program;
