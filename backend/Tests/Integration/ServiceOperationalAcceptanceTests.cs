extern alias WorkerAssembly;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Service.DTOs;
using Modules.Service.Repositories;
using Modules.Service.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using WorkerAssembly::Worker;
using Xunit;

namespace IntegrationTests;

public sealed class ServiceOperationalAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId DeltaTenant = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));

    [Fact(DisplayName = "SV22-SV49 preventive, meter, SLA, contract and offline workflows remain idempotent")]
    public async Task PreventiveMeterSlaContractAndOfflineWorkflowsAreDurable()
    {
        var connectionString = ServiceAcceptanceDatabaseGuard.RequireConfiguredConnection();
        await ServiceAcceptanceDatabaseGuard.VerifyAsync(connectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        var service = factory.Services.GetRequiredService<ServiceService>();
        var actor = await FindActorAsync(connectionString);
        var entity = await ScalarAsync<Guid>(connectionString, "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1", AtlasTenant.Value);
        var customer = await ScalarAsync<Guid>(connectionString, "select p.id from foundation_directory.party p join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id where p.tenant_id=$1 and p.status='Active' and r.role_code='Customer' and r.status='Active' order by p.id limit 1", AtlasTenant.Value);
        var installedBase = await ScalarAsync<Guid>(connectionString, "select id from business_service.installed_base_item where tenant_id=$1 and state='Active' order by id limit 1", AtlasTenant.Value);
        var now = DateTimeOffset.UtcNow;

        var work = await service.CreateWorkOrderAsync(AtlasTenant, actor, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(entity, "CustomerFieldService", "SV operational acceptance", "Workflow proof", customer, installedBase, null, null, null, BillingOwner: "Service"), CancellationToken.None);
        Assert.Null(await service.GetWorkOrderAsync(DeltaTenant, work.Id, CancellationToken.None));
        var ready = await service.TransitionWorkOrderAsync(AtlasTenant, actor, work.Id, "ready", new TransitionRequest(work.Version), CancellationToken.None);
        var skillCode = $"HVAC-{Guid.CreateVersion7():N}";
        var territoryCode = $"TERR-{Guid.CreateVersion7():N}";
        var crewCode = $"CREW-{Guid.CreateVersion7():N}";
        var requirement = await service.AddResourceRequirementAsync(AtlasTenant, actor, work.Id, new CreateResourceRequirementRequest(skillCode, 1m, now, now.AddHours(2)), CancellationToken.None);
        Assert.Equal("Open", requirement.State);
        Assert.Single(await service.ListResourceRequirementsAsync(AtlasTenant, work.Id, CancellationToken.None));
        Assert.Equal(skillCode, (await service.CreateSkillAsync(AtlasTenant, actor, new CreateSkillRequest(skillCode, "HVAC technician"), CancellationToken.None)).Code);
        Assert.Equal(territoryCode, (await service.CreateTerritoryAsync(AtlasTenant, actor, new CreateTerritoryRequest(territoryCode, "Maintenance territory"), CancellationToken.None)).Code);
        Assert.Equal(crewCode, (await service.CreateCrewAsync(AtlasTenant, actor, new CreateCrewRequest(entity, crewCode, "Field Crew"), CancellationToken.None)).Code);
        var externalReference = await service.CreateExternalReferenceAsync(AtlasTenant, actor, new CreateExternalReferenceRequest(work.Id, "WorkOrder", "purchasing", "PurchaseOrder", $"PO-{work.Id:D}", "{\"purpose\":\"external vendor repair\"}"), CancellationToken.None);
        Assert.Single(await service.ListExternalReferencesAsync(AtlasTenant, work.Id, CancellationToken.None));
        Assert.Equal("purchasing", externalReference.SourceModule);
        var visit = await service.CreateVisitAsync(AtlasTenant, actor, work.Id, new CreateVisitRequest(now.AddHours(1), now.AddHours(2)), CancellationToken.None);
        _ = await service.AssignAsync(AtlasTenant, actor, visit.Id, new CreateAssignmentRequest(null, null, DisplayName: "Acceptance Technician"), CancellationToken.None);
        var dispatched = await service.TransitionVisitAsync(AtlasTenant, actor, visit.Id, "dispatch", new TransitionRequest(visit.Version), CancellationToken.None);
        var traveling = await service.TransitionVisitAsync(AtlasTenant, actor, visit.Id, "travel", new TransitionRequest(dispatched.Version), CancellationToken.None);
        var arrived = await service.TransitionVisitAsync(AtlasTenant, actor, visit.Id, "arrive", new TransitionRequest(traveling.Version), CancellationToken.None);
        var inProgress = await service.TransitionVisitAsync(AtlasTenant, actor, visit.Id, "start", new TransitionRequest(arrived.Version), CancellationToken.None);
        var completedVisit = await service.TransitionVisitAsync(AtlasTenant, actor, visit.Id, "complete", new TransitionRequest(inProgress.Version), CancellationToken.None);
        Assert.Equal("Completed", completedVisit.State);
        var scheduledWork = await service.GetWorkOrderAsync(AtlasTenant, work.Id, CancellationToken.None) ?? throw new InvalidOperationException("Scheduled work order is required.");
        var startedWork = await service.TransitionWorkOrderAsync(AtlasTenant, actor, work.Id, "start", new TransitionRequest(scheduledWork.Version), CancellationToken.None);
        var technicalWork = await service.TransitionWorkOrderAsync(AtlasTenant, actor, work.Id, "technical-complete", new TransitionRequest(startedWork.Version), CancellationToken.None);
        var acceptedWork = await service.TransitionWorkOrderAsync(AtlasTenant, actor, work.Id, "accept", new TransitionRequest(technicalWork.Version), CancellationToken.None);
        var closedWork = await service.TransitionWorkOrderAsync(AtlasTenant, actor, work.Id, "close", new TransitionRequest(acceptedWork.Version), CancellationToken.None);
        Assert.Equal("Closed", closedWork.State);
        var labor = await service.RecordLaborAsync(AtlasTenant, actor, new RecordLaborRequest(visit.Id, null, now.AddMinutes(-45), now.AddMinutes(-15), "Travel", 100m, IdempotencyKey: $"sv-op-labor:{work.Id:D}"), CancellationToken.None);
        Assert.Equal(50m, labor.CostAmount);
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, "select count(*) from business_service.service_cost_fact where tenant_id=$1 and work_order_id=$2 and source_type='Labor' and amount=50", AtlasTenant.Value, work.Id));
        var signedFileId = Guid.CreateVersion7();
        var report = await service.SaveReportAsync(AtlasTenant, actor, work.Id, new CreateReportRequest("{\"result\":\"pass\"}", signedFileId), CancellationToken.None);
        Assert.Equal(signedFileId, report.SignedFileId);
        var acceptedReport = await service.AcceptReportAsync(AtlasTenant, actor, report.Id, report.Version, CancellationToken.None);
        Assert.Equal("Accepted", acceptedReport.State);
        await Assert.ThrowsAsync<ServiceException>(() => service.SaveReportAsync(AtlasTenant, actor, work.Id, new CreateReportRequest("{\"result\":\"tampered\"}"), CancellationToken.None));

        var template = await service.CreateTemplateAsync(AtlasTenant, actor,
            new CreateTemplateRequest(entity, $"SV-TEMPLATE-{Guid.CreateVersion7():N}", "Acceptance checklist", 1, 1.5m,
                "{\"questions\":[{\"key\":\"pressure\",\"type\":\"Measurement\",\"uom\":\"bar\"}]}"), CancellationToken.None);
        _ = await service.CreateTemplateAsync(AtlasTenant, actor,
            new CreateTemplateRequest(entity, template.Code, "Acceptance checklist v2", 2, 2m, "{\"questions\":[]}"), CancellationToken.None);
        var checklistWork = await service.CreateWorkOrderAsync(AtlasTenant, actor, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(entity, "CustomerFieldService", "SV checklist validation", "Required-answer and template snapshot proof", customer, installedBase, null, null, null,
                BillingOwner: "Service", TemplateId: template.Id, TemplateVersion: 1), CancellationToken.None);
        Assert.Equal(template.Id, checklistWork.TemplateId);
        Assert.Equal(1, checklistWork.TemplateVersion);
        var checklistReady = await service.TransitionWorkOrderAsync(AtlasTenant, actor, checklistWork.Id, "ready", new TransitionRequest(checklistWork.Version), CancellationToken.None);
        var checklistStarted = await service.TransitionWorkOrderAsync(AtlasTenant, actor, checklistWork.Id, "start", new TransitionRequest(checklistReady.Version), CancellationToken.None);
        await Assert.ThrowsAsync<ServiceException>(() => service.SaveChecklistAnswerAsync(AtlasTenant, actor,
            new ChecklistAnswerRequest(checklistWork.Id, "pressure", "Measurement", "{\"value\":12}", true), CancellationToken.None));
        await Assert.ThrowsAsync<ServiceException>(() => service.SaveChecklistAnswerAsync(AtlasTenant, actor,
            new ChecklistAnswerRequest(checklistWork.Id, "pressure", "Measurement", "{\"value\":12,\"uom\":\"\"}", true), CancellationToken.None));
        _ = await service.SaveChecklistAnswerAsync(AtlasTenant, actor,
            new ChecklistAnswerRequest(checklistWork.Id, "pressure", "Measurement", "{\"value\":12,\"uom\":\"bar\"}", true), CancellationToken.None);
        var checklistTechnical = await service.TransitionWorkOrderAsync(AtlasTenant, actor, checklistWork.Id, "technical-complete", new TransitionRequest(checklistStarted.Version), CancellationToken.None);
        Assert.Equal("TechnicalCompleted", checklistTechnical.State);

        var oldComponent = await service.CreateInstalledBaseAsync(AtlasTenant, actor,
            new CreateInstalledBaseRequest(entity, "Acceptance old component", $"OLD-{Guid.CreateVersion7():N}", null, null, customer, null, installedBase, "Plant"), CancellationToken.None);
        var newComponent = await service.CreateInstalledBaseAsync(AtlasTenant, actor,
            new CreateInstalledBaseRequest(entity, "Acceptance replacement component", $"NEW-{Guid.CreateVersion7():N}", null, null, customer, null, null, "Plant"), CancellationToken.None);
        _ = await service.RecordComponentEventAsync(AtlasTenant, actor,
            new ComponentEventRequest(installedBase, newComponent.Id, "Replaced", now, oldComponent.Id, checklistWork.Id, "SV component replacement"), CancellationToken.None);
        var installedBaseRows = (await service.ListInstalledBaseAsync(AtlasTenant, customer, null, 1, 200, CancellationToken.None)).Items;
        Assert.Equal(installedBase, installedBaseRows.Single(item => item.Id == newComponent.Id).ParentId);
        Assert.Null(installedBaseRows.Single(item => item.Id == oldComponent.Id).ParentId);
        var downtime = await service.StartDowntimeAsync(AtlasTenant, actor, new DowntimeRequest(installedBase, work.Id, "Unplanned", now.AddHours(-3), "Acceptance outage"), CancellationToken.None);
        var endedDowntime = await service.EndDowntimeAsync(AtlasTenant, actor, downtime.Id, now.AddHours(-2), CancellationToken.None);
        Assert.NotNull(endedDowntime.EndedAt);

        var round = await service.CreateMaintenanceRoundAsync(AtlasTenant, actor,
            new CreateMaintenanceRoundRequest(entity, $"SV-ROUND-{Guid.CreateVersion7():N}", "Monthly safety round", DateOnly.FromDateTime(now.UtcDateTime).AddDays(1)), CancellationToken.None);
        var roundSubject = await service.AddRoundSubjectAsync(AtlasTenant, actor, round.Id, new AddRoundSubjectRequest("InstalledBase", installedBase, 1), CancellationToken.None);
        var plannedRound = await service.TransitionRoundAsync(AtlasTenant, actor, round.Id, "plan", new TransitionRequest(round.Version), CancellationToken.None);
        var startedRound = await service.TransitionRoundAsync(AtlasTenant, actor, round.Id, "start", new TransitionRequest(plannedRound.Version), CancellationToken.None);
        var recordedSubject = await service.RecordRoundSubjectAsync(AtlasTenant, actor, roundSubject.Id, new RecordRoundSubjectRequest("Passed", "Pass", "{\"temperature\":42}"), CancellationToken.None);
        Assert.Equal("Passed", recordedSubject.State);
        var completedRound = await service.TransitionRoundAsync(AtlasTenant, actor, round.Id, "complete", new TransitionRequest(startedRound.Version), CancellationToken.None);
        Assert.Equal("Completed", completedRound.State);
        var recovery = await service.CreateSupplierRecoveryAsync(AtlasTenant, actor, new CreateSupplierRecoveryRequest(work.Id, customer, $"supplier-warranty:{work.Id:D}", 75m, Reason: "Covered compressor part"), CancellationToken.None);
        Assert.Equal("Draft", recovery.State);
        var recovered = await service.TransitionSupplierRecoveryAsync(AtlasTenant, actor, recovery.Id, "Recovered", CancellationToken.None);
        Assert.Equal("Recovered", recovered.State);
        await Assert.ThrowsAsync<ServiceException>(() => service.CreateWarrantyAsync(AtlasTenant, actor,
            new CreateWarrantyRequest(entity, "Supplier", null, null, installedBase, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)), CancellationToken.None));
        var supplierWarranty = await service.CreateWarrantyAsync(AtlasTenant, actor,
            new CreateWarrantyRequest(entity, "Supplier", customer, null, installedBase, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)), CancellationToken.None);
        Assert.Equal("Supplier", supplierWarranty.WarrantyType);

        var fixedAsset = await ScalarAsync<Guid>(connectionString, "select id from business_assets.fixed_asset where tenant_id=$1 and legal_entity_id=$2 order by id limit 1", AtlasTenant.Value, entity);
        var internalWork = await service.CreateWorkOrderAsync(AtlasTenant, actor, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(entity, "InternalMaintenance", "SV internal asset maintenance", "History projection proof", null, null, fixedAsset, null, null), CancellationToken.None);
        var internalReady = await service.TransitionWorkOrderAsync(AtlasTenant, actor, internalWork.Id, "ready", new TransitionRequest(internalWork.Version), CancellationToken.None);
        var internalStarted = await service.TransitionWorkOrderAsync(AtlasTenant, actor, internalWork.Id, "start", new TransitionRequest(internalReady.Version), CancellationToken.None);
        var internalTechnical = await service.TransitionWorkOrderAsync(AtlasTenant, actor, internalWork.Id, "technical-complete", new TransitionRequest(internalStarted.Version), CancellationToken.None);
        Assert.Equal("TechnicalCompleted", internalTechnical.State);
        Assert.True(await ScalarAsync<long>(connectionString, "select count(*) from business_service.service_fixed_asset_history where tenant_id=$1 and fixed_asset_id=$2 and work_order_id=$3 and event_type='TechnicalCompleted'", AtlasTenant.Value, fixedAsset, internalWork.Id) == 1);

        var plan = await service.CreatePlanAsync(AtlasTenant, actor, new CreateMaintenancePlanRequest(entity, $"SV-Calendar-{Guid.CreateVersion7():N}", "InstalledBase", installedBase, "CalendarFixed", new DateOnly(2026, 11, 1), 30, null, null), CancellationToken.None);
        var generated = await service.GenerateDueAsync(AtlasTenant, actor, new GenerateDueRequest(new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 1)), CancellationToken.None);
        Assert.True(generated.CreatedDueItems > 0);
        var replayedGeneration = await service.GenerateDueAsync(AtlasTenant, actor, new GenerateDueRequest(new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 1)), CancellationToken.None);
        Assert.Equal(0, replayedGeneration.CreatedDueItems);
        var due = (await service.ListDueAsync(AtlasTenant, "Due", new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 1), 1, 100, CancellationToken.None)).Items.First(item => item.PlanId == plan.Id);
        var postponed = await service.UpdateDueAsync(AtlasTenant, actor, due.Id, new MaintenanceDueUpdateRequest("Postponed", "Capacity", due.Version), CancellationToken.None);
        await Assert.ThrowsAsync<ServiceException>(() => service.UpdateDueAsync(AtlasTenant, actor, due.Id, new MaintenanceDueUpdateRequest("Skipped", "stale", due.Version), CancellationToken.None));
        var converted = await service.ConvertDueAsync(AtlasTenant, actor, due.Id, new ConvertDueRequest(entity, "CustomerFieldService", "SV preventive work", "Generated from plan", customer, null), CancellationToken.None);
        Assert.Equal("Converted", converted.Due.State);
        Assert.True(await ScalarAsync<long>(connectionString, "select count(*) from business_service.maintenance_due where tenant_id=$1 and plan_id=$2 and state='Forecast'", AtlasTenant.Value, plan.Id) >= 1);

        var meterCode = $"SV-METER-{Guid.CreateVersion7():N}";
        var firstReading = await service.RecordMeterAsync(AtlasTenant, actor, new RecordMeterReadingRequest(entity, installedBase, meterCode, 100m, "H", now.AddDays(-3)), CancellationToken.None);
        var duplicateReading = await service.RecordMeterAsync(AtlasTenant, actor, new RecordMeterReadingRequest(entity, installedBase, meterCode, 100m, "H", now.AddDays(-3)), CancellationToken.None);
        Assert.Equal(firstReading.Id, duplicateReading.Id);
        await ExecuteAsync(connectionString, AtlasTenant, "update business_service.meter_definition set rollover_value=20 where tenant_id=$1 and subject_id=$2 and meter_code=$3", installedBase, meterCode);
        await Assert.ThrowsAsync<ServiceException>(() => service.RecordMeterAsync(AtlasTenant, actor, new RecordMeterReadingRequest(entity, installedBase, meterCode, 50m, "H", now.AddDays(-2)), CancellationToken.None));
        var reset = await service.RecordMeterAsync(AtlasTenant, actor, new RecordMeterReadingRequest(entity, installedBase, meterCode, 0m, "H", now.AddDays(-1), IsReset: true), CancellationToken.None);
        Assert.True(reset.IsReset);
        _ = await service.RecordMeterAsync(AtlasTenant, actor, new RecordMeterReadingRequest(entity, installedBase, meterCode, 10m, "H", now), CancellationToken.None);
        var meterPlan = await service.CreatePlanAsync(AtlasTenant, actor, new CreateMaintenancePlanRequest(entity, $"SV-Meter-{Guid.CreateVersion7():N}", "InstalledBase", installedBase, "Meter", null, null, 1m, meterCode), CancellationToken.None);
        var meterGeneration = await service.GenerateDueAsync(AtlasTenant, actor, new GenerateDueRequest(new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 1)), CancellationToken.None);
        Assert.True(meterGeneration.InspectedPlans >= 2);
        Assert.True(await ScalarAsync<long>(connectionString, "select count(*) from business_service.maintenance_due where tenant_id=$1 and plan_id=$2", AtlasTenant.Value, meterPlan.Id) >= 1);

        var profile = await service.CreateSlaProfileAsync(AtlasTenant, actor, new CreateSlaProfileRequest(entity, $"SV-SLA-{Guid.CreateVersion7():N}", 30, 60, 90), CancellationToken.None);
        var breachedClock = await service.StartSlaClockAsync(AtlasTenant, actor, new StartSlaClockRequest(work.Id, profile.Id, "Resolution", now.AddDays(-2)), CancellationToken.None);
        var breach = await service.MonitorSlaAsync(AtlasTenant, actor, CancellationToken.None);
        Assert.True(breach.BreachedClocks >= 1);
        var pausedClock = await service.StartSlaClockAsync(AtlasTenant, actor, new StartSlaClockRequest(work.Id, profile.Id, "Response", now), CancellationToken.None);
        var paused = await service.PauseSlaClockAsync(AtlasTenant, actor, work.Id, "Response", new SlaClockActionRequest("Customer requested pause", now.AddMinutes(-10)), CancellationToken.None);
        var resumed = await service.ResumeSlaClockAsync(AtlasTenant, actor, work.Id, "Response", new SlaClockActionRequest("Customer available", now), CancellationToken.None);
        Assert.Equal("Running", resumed.State);
        Assert.True(resumed.PausedMinutes >= 9);

        var contract = await service.CreateContractAsync(AtlasTenant, actor, new CreateContractRequest(entity, customer, customer, new DateOnly(2026, 1, 1), new DateOnly(2099, 12, 31), ScopeJson: "{\"coveredCategories\":[\"Labor\"],\"includedLaborAmount\":100}"), CancellationToken.None);
        await service.ActivateContractAsync(AtlasTenant, actor, contract.Id, CancellationToken.None);
        var usage = await service.RecordContractUsageAsync(AtlasTenant, actor, contract.Id, new ContractUsageRequest(work.Id, "Visits", 1m, $"sv-op-usage:{work.Id:D}"), CancellationToken.None);
        _ = await service.RecordContractUsageAsync(AtlasTenant, actor, contract.Id, new ContractUsageRequest(work.Id, "Visits", 1m, $"sv-op-usage:{work.Id:D}"), CancellationToken.None);
        var entitlement = await service.ResolveEntitlementAsync(AtlasTenant, actor, new EntitlementRequest(entity, customer, null, work.Id, new DateOnly(2026, 11, 16), [new EntitlementLineRequest("Labor", 150m)]), CancellationToken.None);
        Assert.Equal(100m, entitlement.Lines.Single().CoveredAmount);
        Assert.Equal(50m, entitlement.Lines.Single().CustomerAmount);

        var offlineId = Guid.CreateVersion7();
        var offlineRequest = new OfflineCommandRequest(offlineId, "checklist.answer", work.Id, null, null, $"{{\"workOrderId\":\"{work.Id}\",\"questionKey\":\"sv-op\",\"questionType\":\"Boolean\",\"answerJson\":\"true\",\"required\":true}}");
        var offline = await service.SubmitOfflineAsync(AtlasTenant, actor, Guid.CreateVersion7(), offlineRequest, CancellationToken.None);
        var offlineReplay = await service.SubmitOfflineAsync(AtlasTenant, actor, Guid.CreateVersion7(), offlineRequest, CancellationToken.None);
        Assert.Equal("Processed", offline.State);
        Assert.Equal(offline.Id, offlineReplay.Id);
        var conflict = await service.SubmitOfflineAsync(AtlasTenant, actor, Guid.CreateVersion7(), new OfflineCommandRequest(Guid.CreateVersion7(), "checklist.answer", work.Id, null, -1, offlineRequest.PayloadJson), CancellationToken.None);
        Assert.Equal("Conflict", conflict.State);
        Assert.Equal("service.concurrent_change", conflict.ConflictCode);

        var repository = factory.Services.GetRequiredService<ServiceScheduledWorkRepository>();
        Assert.Contains(await repository.ListScheduledScopesAsync(CancellationToken.None), scope => scope.Tenant == AtlasTenant && scope.LegalEntityId == entity);
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var worker = new ServiceMaintenanceWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, loggerFactory.CreateLogger<ServiceMaintenanceWorker>());
        await worker.RunCycleAsync(factory.Services, CancellationToken.None);
        Assert.True(await ScalarAsync<long>(connectionString, "select count(*) from business_service.sla_clock where tenant_id=$1 and state='Breached'", AtlasTenant.Value) >= 1);
        var kpi = await service.GetKpiAsync(AtlasTenant, CancellationToken.None);
        Assert.True(kpi.FailureCount >= 1);
        Assert.True(kpi.MtbfHours >= 0m && kpi.MttrHours >= 0m && kpi.AvailabilityPercent is >= 0m and <= 100m);
    }

    private static async Task<Guid> FindActorAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SetTenantAsync(connection, AtlasTenant.Value);
        await using var command = new NpgsqlCommand("select i.id from platform.identity i where i.tenant_id=$1 and i.email='admin@atlas.com' and i.status='Active' limit 1", connection);
        command.Parameters.AddWithValue(AtlasTenant.Value);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Atlas administrator is required."));
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SetTenantAsync(connection, AtlasTenant.Value);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameterValue in values) command.Parameters.AddWithValue(parameterValue);
        var result = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a database scalar.");
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, TenantId tenant, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SetTenantAsync(connection, tenant.Value);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}
