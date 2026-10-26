using Microsoft.AspNetCore.Http;

namespace Shared.BuildingBlocks.Observability;

public static class RequestCorrelationId
{
    private static readonly object ContextKey = new();

    public static Guid GetOrCreate(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(ContextKey, out var existing) && existing is Guid cached && cached != Guid.Empty)
            return cached;

        var header = context.Request.Headers["X-Correlation-Id"];
        var value = header.Count == 1 ? header[0] : null;
        var correlationId = value is { Length: <= 64 } && Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : Guid.CreateVersion7();

        context.Items[ContextKey] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId.ToString("D");
        return correlationId;
    }
}
