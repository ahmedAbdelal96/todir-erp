using System.Text.Json;
using System.Globalization;
using Core.Messaging;
using MassTransit;
using Quartz;
using Shared.Contracts.Hr;
using Shared.Contracts.Platform;
using Shared.Primitives.Identifiers;

namespace Worker;

/// <summary>Schedules IAM access removal for the effective HR date; HR remains the source of employment truth.</summary>
public sealed class HrMembershipDeactivationConsumer(ISchedulerFactory schedulers, IInboxStore inboxStore)
    : IConsumer<OutboxPublishedMessage>
{
    public const string EndpointName = "hr-membership-deactivation";
    private const string MessageType = "HrMembershipDeactivationRequestedV1";

    public async Task Consume(ConsumeContext<OutboxPublishedMessage> context)
    {
        var message = context.Message;
        if (message.MessageType != MessageType) return;
        if (!await inboxStore.TryBeginAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken)) return;
        try
        {
            var request = JsonSerializer.Deserialize<HrMembershipDeactivationRequestedV1>(message.Payload)
                ?? throw new InvalidOperationException("The HR membership deactivation event payload is empty.");
            if (request.MembershipId == Guid.Empty || request.EmployeeId == Guid.Empty || request.EffectiveOn == default)
                throw new InvalidOperationException("The HR membership deactivation event is missing required identifiers or its effective date.");

            var key = new JobKey($"deactivate-membership-{message.MessageId:N}", "hr-lifecycle");
            var job = JobBuilder.Create<HrMembershipDeactivationJob>().WithIdentity(key).UsingJobData("TenantId", message.TenantId.Value.ToString("D"))
                .UsingJobData("EmployeeId", request.EmployeeId.ToString("D")).UsingJobData("MembershipId", request.MembershipId.ToString("D"))
                .UsingJobData("EffectiveOn", request.EffectiveOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .UsingJobData("ReasonCode", request.ReasonCode).Build();
            var effectiveAt = new DateTimeOffset(request.EffectiveOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var trigger = TriggerBuilder.Create().WithIdentity($"{key.Name}-trigger", key.Group).ForJob(key).StartAt(effectiveAt).Build();
            var scheduler = await schedulers.GetScheduler(context.CancellationToken);
            if (!await scheduler.CheckExists(key, context.CancellationToken))
                await scheduler.ScheduleJob(job, trigger, context.CancellationToken);
            await inboxStore.MarkProcessedAsync(EndpointName, message.MessageId, message.TenantId, context.CancellationToken);
        }
        catch
        {
            await inboxStore.AbandonAsync(EndpointName, message.MessageId, message.TenantId, CancellationToken.None);
            throw;
        }
    }
}

public sealed class HrMembershipDeactivationConsumerDefinition : ConsumerDefinition<HrMembershipDeactivationConsumer>
{
    public HrMembershipDeactivationConsumerDefinition() => EndpointName = HrMembershipDeactivationConsumer.EndpointName;
}

/// <summary>Rechecks effective-dated HR truth at execution time to protect transfers and rehires.</summary>
public sealed class HrMembershipDeactivationJob(IHumanResourcesLifecycleCapability hr, IPlatformMembershipLifecycleCapability iam, TimeProvider clock) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        if (!Guid.TryParse(data.GetString("TenantId"), out var tenantId)
            || !Guid.TryParse(data.GetString("EmployeeId"), out var employeeId)
            || !Guid.TryParse(data.GetString("MembershipId"), out var membershipId)
            || !DateOnly.TryParse(data.GetString("EffectiveOn"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveOn)
            || string.IsNullOrWhiteSpace(data.GetString("ReasonCode")))
            throw new JobExecutionException("The scheduled HR membership deactivation data is invalid.");
        if (effectiveOn > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            throw new JobExecutionException("The scheduled HR membership deactivation fired before its effective date.");
        if (await hr.ShouldDeactivateMembershipAsync(new TenantId(tenantId), employeeId, membershipId, effectiveOn, context.CancellationToken))
            await iam.DeactivateMembershipAsync(new TenantId(tenantId), membershipId, data.GetString("ReasonCode")!, context.CancellationToken);
    }
}
