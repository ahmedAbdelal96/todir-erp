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
    private const string Endpoint = "platform-outbox-probe";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var started = Stopwatch.GetTimestamp();
        if (!await inboxStore.TryBeginAsync(Endpoint, context.Message.MessageId, context.Message.TenantId, context.CancellationToken))
        {
            TodirMetrics.InboxDuplicate.Add(1, new KeyValuePair<string, object?>("endpoint", Endpoint));
            return;
        }
        await inboxStore.MarkProcessedAsync(Endpoint, context.Message.MessageId, context.Message.TenantId, context.CancellationToken);
        TodirMetrics.InboxProcessed.Add(1, new KeyValuePair<string, object?>("endpoint", Endpoint));
        TodirMetrics.MessageProcessingDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>("endpoint", Endpoint));
        FailureInjection.CrashAfterInboxCommitIfConfigured();
    }

    private static class FailureInjection
    {
        public static void CrashAfterInboxCommitIfConfigured()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION"), "crash-after-inbox-commit", StringComparison.OrdinalIgnoreCase)) return;
            var markerPath = Environment.GetEnvironmentVariable("TODIR_FAILURE_INJECTION_MARKER");
            if (string.IsNullOrWhiteSpace(markerPath)) return;
            var directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            try
            {
                using var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough);
                marker.Flush(flushToDisk: true);
            }
            catch (IOException)
            {
                return;
            }
            Environment.FailFast("Controlled failure injection after Inbox commit and before broker acknowledgement.");
        }
    }
}

public sealed class OutboxPublishedMessageConsumerDefinition : ConsumerDefinition<OutboxPublishedMessageConsumer>
{
    public OutboxPublishedMessageConsumerDefinition()
    {
        EndpointName = "platform-outbox-probe";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<OutboxPublishedMessageConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(8, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2)));
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
