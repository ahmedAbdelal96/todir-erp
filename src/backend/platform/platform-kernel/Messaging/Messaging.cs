using MassTransit;
using Todir.SharedKernel.Identifiers;
using Todir.BuildingBlocks.Observability;
using System.Diagnostics;

namespace Todir.Platform.Messaging;

public enum ActorType { Human, System, Automation, AI, Service, Support }
public sealed record MessageEnvelope<T>(Guid MessageId, string MessageType, int SchemaVersion, DateTimeOffset OccurredAtUtc, TenantId TenantId, Guid? LegalEntityId, Guid? BranchId, Guid CorrelationId, Guid CausationId, Guid? ActorId, ActorType ActorType, string SourceModule, string? SourceRef, string? TraceParent, T Payload);
public sealed record InboxKey(string ConsumerEndpoint, Guid MessageId);
public interface IMessagePublisher { Task PublishAsync<T>(MessageEnvelope<T> message, CancellationToken cancellationToken = default); }
public interface IMessageConsumer { Task ConsumeAsync(MessageEnvelope<JsonElementPayload> message, CancellationToken cancellationToken = default); }
public readonly record struct JsonElementPayload(string Json);
public sealed record OutboxPublishedMessage(Guid MessageId, string MessageType, string Payload, TenantId TenantId, DateTimeOffset OccurredAt, Guid CorrelationId, Guid CausationId, string SourceModule, string? TraceParent, int Attempts = 0);

public sealed class OutboxPublishedMessageConsumer(IInboxStore inboxStore) : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "platform-outbox-probe";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var started = Stopwatch.GetTimestamp();
        FailureInjection.ThrowIfPoison(context.Message.MessageType);
        if (!await inboxStore.TryBeginAsync(EndpointName, context.Message.MessageId, context.Message.TenantId, context.CancellationToken))
        {
            TodirMetrics.InboxDuplicate.Add(1, new KeyValuePair<string, object?>("endpoint", EndpointName));
            return;
        }
        await inboxStore.MarkProcessedAsync(EndpointName, context.Message.MessageId, context.Message.TenantId, context.CancellationToken);
        TodirMetrics.InboxProcessed.Add(1, new KeyValuePair<string, object?>("endpoint", EndpointName));
        TodirMetrics.MessageProcessingDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("endpoint", EndpointName));
        FailureInjection.CrashAfterInboxCommit();
    }

    private static class FailureInjection
    {
        public static void ThrowIfPoison(string messageType)
        {
            var configured = Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION") ?? string.Empty;
            if (configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("poison-message", StringComparer.OrdinalIgnoreCase)
                && string.Equals(messageType, "StagingPoisonV1", StringComparison.Ordinal)
                && ShouldPoison(Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION_POISON_MARKER")))
                throw new InvalidOperationException("controlled.poison_message");
        }

        public static void CrashAfterInboxCommit()
        {
            var configured = Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION") ?? string.Empty;
            if (!configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("crash-after-inbox-commit", StringComparer.OrdinalIgnoreCase)) return;
            var markerPath = Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION_MARKER");
            if (!CreateMarker(markerPath)) return;
            Environment.FailFast("Controlled failure injection after Inbox commit and before broker acknowledgement.");
        }

        private static bool CreateMarker(string? markerPath)
        {
            if (string.IsNullOrWhiteSpace(markerPath)) return false;
            var directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            try
            {
                using var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough);
                marker.Flush(flushToDisk: true);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool ShouldPoison(string? markerPath)
        {
            if (string.IsNullOrWhiteSpace(markerPath) || File.Exists($"{markerPath}.allow")) return false;
            CreateMarker(markerPath);
            return true;
        }
    }
}

public sealed class OutboxPublishedMessageConsumerDefinition : ConsumerDefinition<OutboxPublishedMessageConsumer>
{
    public OutboxPublishedMessageConsumerDefinition()
    {
        EndpointName = OutboxPublishedMessageConsumer.EndpointName;
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<OutboxPublishedMessageConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(100)));
    }
}
public sealed record MessageRetryDecision(bool Retry, DateTimeOffset? NextAttemptAt, string? ErrorCode);
public interface IMessageRetryPolicy
{
    MessageRetryDecision Decide(int currentAttempt, DateTimeOffset now, Exception exception);
}
public sealed class ExponentialMessageRetryPolicy : IMessageRetryPolicy
{
    private readonly int maxAttempts;
    private readonly TimeSpan maximumDelay;

    public ExponentialMessageRetryPolicy(int maxAttempts = 8, TimeSpan? maximumDelay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1, nameof(maxAttempts));
        this.maxAttempts = maxAttempts;
        this.maximumDelay = maximumDelay ?? TimeSpan.FromMinutes(30);
    }

    public MessageRetryDecision Decide(int currentAttempt, DateTimeOffset now, Exception exception)
    {
        if (currentAttempt >= maxAttempts) return new(false, null, "messaging.retry_exhausted");
        var seconds = Math.Min(maximumDelay.TotalSeconds, Math.Pow(2, Math.Min(currentAttempt, 10)));
        return new(true, now.AddSeconds(seconds), exception.GetType().Name);
    }
}

public sealed record MessageError(Guid MessageId, TenantId TenantId, string ConsumerEndpoint, int Attempt, string ErrorCode, string Detail, DateTimeOffset OccurredAt, bool Resolved);
public interface IMessageErrorStore
{
    Task RecordAsync(MessageError messageError, CancellationToken cancellationToken = default);
    Task MarkResolvedAsync(Guid messageId, string consumerEndpoint, TenantId tenantId, CancellationToken cancellationToken = default);
}
public sealed class InMemoryMessageErrorStore : IMessageErrorStore
{
    private readonly List<MessageError> errors = [];
    private readonly object gate = new();
    public Task RecordAsync(MessageError messageError, CancellationToken cancellationToken = default) { lock (gate) errors.Add(messageError); return Task.CompletedTask; }
    public Task MarkResolvedAsync(Guid messageId, string consumerEndpoint, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            for (var index = 0; index < errors.Count; index++)
                if (errors[index].MessageId == messageId && errors[index].ConsumerEndpoint == consumerEndpoint) errors[index] = errors[index] with { Resolved = true };
        }
        return Task.CompletedTask;
    }
    public IReadOnlyList<MessageError> Snapshot() { lock (gate) return errors.ToArray(); }
}
public sealed class InMemoryMessagePublisher : IMessagePublisher
{
    private readonly List<object> messages = [];
    private readonly object gate = new();
    public Task PublishAsync<T>(MessageEnvelope<T> message, CancellationToken cancellationToken = default) { lock (gate) messages.Add(message); return Task.CompletedTask; }
    public IReadOnlyList<object> Snapshot() { lock (gate) return messages.ToArray(); }
}
