using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shared.Primitives.Identifiers;

namespace Modules.Expenses.Services;

public sealed record ExpenseOcrResult(System.Text.Json.JsonElement Suggestions);

/// <summary>Provider seam for asynchronous receipt suggestions; suggestions never mutate financial facts.</summary>
public interface IExpenseOcrProvider
{
    Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default);
}

public sealed class UnavailableExpenseOcrProvider : IExpenseOcrProvider
{
    public Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default) =>
        throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", "No approved OCR provider is configured.");
}

/// <summary>
/// Provider-neutral HTTP boundary for an approved OCR service. The service returns suggestions only;
/// the Expenses aggregate remains the sole authority for financial facts. An unset endpoint fails closed.
/// </summary>
public sealed class ConfiguredExpenseOcrProvider(HttpClient client, string? endpoint) : IExpenseOcrProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExpenseOcrResult> ExtractAsync(TenantId tenant, Guid fileReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", "No approved OCR provider is configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Trim())
        {
            Content = JsonContent.Create(new { tenantId = tenant.Value, fileReference }, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant.Value.ToString("D"));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExpenseOcrProviderException("expenses.ocr_provider_timeout", "The OCR provider timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", ex.Message);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.RequestTimeout or (HttpStatusCode)429 || (int)response.StatusCode >= 500)
                throw new ExpenseOcrProviderException("expenses.ocr_provider_unavailable", $"OCR provider returned {(int)response.StatusCode}.");
            if (!response.IsSuccessStatusCode)
                throw new ExpenseOcrProviderException("expenses.ocr_provider_rejected", $"OCR provider returned {(int)response.StatusCode}.");

            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                var root = document.RootElement;
                var suggestions = root.TryGetProperty("suggestions", out var property) ? property : root;
                return new ExpenseOcrResult(suggestions.Clone());
            }
            catch (JsonException ex)
            {
                throw new ExpenseOcrProviderException("expenses.ocr_provider_invalid_response", ex.Message);
            }
        }
    }
}

public sealed class ExpenseOcrProviderException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
