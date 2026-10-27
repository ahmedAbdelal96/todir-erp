using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using Modules.Payments.DTOs;

namespace Modules.Payments.Services;

public interface IReferencePaymentProvider
{
    Task<ReferenceProviderResult> CreatePaymentRequestAsync(Guid requestId, string merchantReference, decimal amount,
        string currencyCode, string idempotencyKey, CancellationToken cancellationToken);
    Task<ReferenceProviderResult> GetPaymentStatusAsync(Guid requestId, string merchantReference, string? transactionId,
        CancellationToken cancellationToken);
    Task<ReferenceProviderCancelResult> CancelPaymentRequestAsync(Guid requestId, string? transactionId, string idempotencyKey,
        CancellationToken cancellationToken);
    Task<ReferenceProviderRefundResult> RefundPaymentAsync(Guid operationId, string providerTransactionId, decimal amount,
        string currencyCode, string idempotencyKey, CancellationToken cancellationToken);
    Task<ReferenceProviderRefundResult> GetRefundStatusAsync(Guid operationId, CancellationToken cancellationToken);
    void ObserveVerifiedWebhook(Guid requestId, string transactionId, string status, DateTimeOffset occurredAt);
}

public interface IProviderSecretResolver
{
    string? Resolve(string secretReference);
}

public sealed class EnvironmentProviderSecretResolver : IProviderSecretResolver
{
    public string? Resolve(string secretReference) => Environment.GetEnvironmentVariable(SecretEnvironmentVariableName(secretReference));

    public static string SecretEnvironmentVariableName(string secretReference)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(secretReference));
        return "TODIR_PROVIDER_SECRET_" + Convert.ToHexString(digest);
    }
}

/// <summary>Deterministic sandbox provider used to exercise the durable provider boundary without network calls.</summary>
public sealed class ReferencePaymentProvider(TimeProvider clock) : IReferencePaymentProvider
{
    private readonly ConcurrentDictionary<Guid, ReferenceProviderResult> states = new();
    private readonly ConcurrentDictionary<Guid, ReferenceProviderRefundResult> refundStates = new();

    public Task<ReferenceProviderResult> CreatePaymentRequestAsync(Guid requestId, string merchantReference, decimal amount,
        string currencyCode, string idempotencyKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(merchantReference) || amount <= 0m
            || string.IsNullOrWhiteSpace(currencyCode) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Reference provider request is incomplete.");
        if (idempotencyKey.Contains("simulate-timeout", StringComparison.OrdinalIgnoreCase))
        {
            var unknown = new ReferenceProviderResult("Unknown", string.Empty, clock.GetUtcNow());
            states[requestId] = unknown;
            return Task.FromResult(unknown);
        }
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId:D}:{idempotencyKey}"));
        var transactionId = "ref_" + Convert.ToHexString(digest.AsSpan(0, 16)).ToLowerInvariant();
        var result = new ReferenceProviderResult("RequiresAction", transactionId, clock.GetUtcNow());
        states[requestId] = result;
        return Task.FromResult(result);
    }

    public Task<ReferenceProviderResult> GetPaymentStatusAsync(Guid requestId, string merchantReference, string? transactionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(merchantReference)) throw new ArgumentException("Reference provider status request is incomplete.");
        return Task.FromResult(states.TryGetValue(requestId, out var known)
            ? known
            : new ReferenceProviderResult("Unknown", transactionId ?? string.Empty, clock.GetUtcNow()));
    }

    public Task<ReferenceProviderRefundResult> RefundPaymentAsync(Guid operationId, string providerTransactionId, decimal amount,
        string currencyCode, string idempotencyKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(providerTransactionId) || amount <= 0m
            || string.IsNullOrWhiteSpace(currencyCode) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Reference provider refund details are incomplete.");
        if (refundStates.TryGetValue(operationId, out var existing)) return Task.FromResult(existing);
        if (idempotencyKey.Contains("simulate-timeout", StringComparison.OrdinalIgnoreCase))
        {
            var unknown = new ReferenceProviderRefundResult("Unknown", string.Empty, clock.GetUtcNow());
            refundStates[operationId] = unknown;
            return Task.FromResult(unknown);
        }
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:D}:{providerTransactionId}:{idempotencyKey}"));
        var result = new ReferenceProviderRefundResult("Succeeded", "refref_" + Convert.ToHexString(digest.AsSpan(0, 16)).ToLowerInvariant(), clock.GetUtcNow());
        refundStates[operationId] = result;
        return Task.FromResult(result);
    }

    public Task<ReferenceProviderRefundResult> GetRefundStatusAsync(Guid operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(refundStates.TryGetValue(operationId, out var status)
            ? status
            : new ReferenceProviderRefundResult("Unknown", string.Empty, clock.GetUtcNow()));
    }

    public Task<ReferenceProviderCancelResult> CancelPaymentRequestAsync(Guid requestId, string? transactionId, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Reference provider cancellation details are incomplete.");
        if (states.TryGetValue(requestId, out var current) && current.Status == "Succeeded")
            throw new InvalidOperationException("A succeeded provider payment cannot be cancelled.");
        states[requestId] = new("Cancelled", transactionId ?? string.Empty, clock.GetUtcNow());
        return Task.FromResult(new ReferenceProviderCancelResult("Cancelled", clock.GetUtcNow()));
    }

    public void ObserveVerifiedWebhook(Guid requestId, string transactionId, string status, DateTimeOffset occurredAt)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(transactionId)) return;
        var mapped = status switch { "payment.succeeded" => "Succeeded", "payment.failed" => "Failed", "payment.authorized" => "Authorized", _ => "Unknown" };
        if (mapped != "Unknown") states[requestId] = new(mapped, transactionId, occurredAt);
    }
}
