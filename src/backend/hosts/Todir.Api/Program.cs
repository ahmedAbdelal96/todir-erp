using Todir.BuildingBlocks.Observability;
using Todir.Platform.Hosting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTodirObservability(builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
builder.Services.AddTodirPlatform(builder.Configuration);
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("api", limiter =>
{
    limiter.PermitLimit = 120;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var app = builder.Build();

// Keep the approved order explicit: correlation, exception handling, security headers, endpoint.
app.UseTodirCorrelation();
app.UseTodirExceptionHandling();
app.UseTodirSecurityHeaders();
app.UseHttpsRedirection();
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
            await context.Response.WriteAsJsonAsync(new { code = "security.csrf_invalid" });
            return;
        }
    }
    await next(context);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapTodirPlatform();
app.Run();

public partial class Program;
