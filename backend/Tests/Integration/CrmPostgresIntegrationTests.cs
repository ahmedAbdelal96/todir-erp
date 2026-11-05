using System.Text.Json;
using Modules.CRM.DTOs;
using Modules.CRM.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class CrmPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    [CrmPostgresFact]
    public async Task LeadOpportunityActivityForecastLifecyclePersistsAndIsIdempotent()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        var baseline = await repository.ForecastAsync(tenant, fixture.EntityId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), "EGP", default);
        var key = "crm-test-" + Guid.CreateVersion7().ToString("N");
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key, default);
        var replay = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key, default);
        Assert.Equal(lead.Id, replay.Id);
        var duplicate = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة مكرر", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key + "-duplicate", default);
        Assert.Contains(lead.Id, duplicate.DuplicateCandidateIds);
        var qualified = await repository.QualifyLeadAsync(tenant, fixture.ActorId, fixture.EntityId, lead.Id, new(null, "Need", "Budget", "Q4", "Manager", lead.Version), default);
        var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "فرصة اختبار", 12500m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))), default);
        var moved = await repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(fixture.ProposalStageId, opportunity.Version, "proposal"), default);
        var activity = await repository.CreateActivityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, opportunity.Id, null, "Call", "متابعة العميل", DateTimeOffset.UtcNow.AddDays(1), null, fixture.ActorId, "activity-" + Guid.CreateVersion7().ToString("N")), default);
        var completed = await repository.CompleteActivityAsync(tenant, fixture.ActorId, fixture.EntityId, activity.Id, new(activity.Version, "Completed", null, false), default);
        var forecast = await repository.ForecastAsync(tenant, fixture.EntityId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), "EGP", default);
        Assert.Equal("Qualified", qualified.Status);
        Assert.Equal(fixture.ProposalStageId, moved.StageId);
        Assert.Equal("Completed", completed.State);
        Assert.Equal(12500m, forecast.PipelineAmount - baseline.PipelineAmount);
        Assert.Equal(baseline.OpenOpportunityCount + 1, forecast.OpenOpportunityCount);
        var otherTenantCount = await repository.ListLeadsAsync(new TenantId(fixture.OtherTenantId), fixture.OtherEntityId, fixture.ActorId, null, "crm-test@example.test", 1, 50, null, default);
        Assert.Equal(0, otherTenantCount.TotalCount);
    }

    [CrmPostgresFact]
    public async Task TeamVisibilityAndConversionUniquenessAreEnforced()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        var seller = Guid.CreateVersion7();
        var team = await repository.CreateTeamAsync(tenant, new(fixture.EntityId, "TEST-TEAM-" + Guid.CreateVersion7().ToString("N"), "CRM Test Team"), default);
        await repository.AddTeamMemberAsync(tenant, fixture.EntityId, team.Id, new(seller), default);
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "Team lead", null, "team-lead@example.test", null, "Integration", null, team.Id), "team-lead-" + Guid.CreateVersion7().ToString("N"), default);
        var visible = await repository.ListLeadsAsync(tenant, fixture.EntityId, seller, null, "team-lead@example.test", 1, 20, "mine", default);
        Assert.Contains(visible.Items, x => x.Id == lead.Id);
        var qualified = await repository.QualifyLeadAsync(tenant, fixture.ActorId, fixture.EntityId, lead.Id, new(null, "Need", null, null, null, lead.Version), default);
        var first = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "Converted once", 100m, "EGP", null), default);
        var second = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "Converted retry", 100m, "EGP", null), default);
        Assert.Equal(first.Id, second.Id);
        await repository.CreateActivityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, first.Id, null, "Call", "Overdue follow-up", DateTimeOffset.UtcNow.AddDays(-2), null, fixture.ActorId, "attention-" + Guid.CreateVersion7().ToString("N")), default);
        var attention = await repository.ListSellerActivitiesAsync(tenant, fixture.EntityId, fixture.ActorId, "attention", false, 1, 20, default);
        Assert.Contains(attention.Items, x => x.Subject == "Overdue follow-up");
    }

    [CrmPostgresFact]
    public async Task ConfigurationCustomFieldsAndForecastSnapshotPersistWithConcurrencyTokens()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        var fieldKey = "budget_" + Guid.CreateVersion7().ToString("N");
        var definition = await repository.CreateCustomFieldDefinitionAsync(tenant, new(fixture.EntityId, "Lead", fieldKey, "Budget", "Decimal"), default);
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "Custom field lead", null, "custom-field@example.test", null, "Integration"), "custom-field-" + Guid.CreateVersion7().ToString("N"), default);
        using var budget = JsonDocument.Parse("12500.50");
        var values = await repository.SetCustomFieldValuesAsync(tenant, fixture.ActorId, fixture.EntityId, new("Lead", lead.Id, new Dictionary<string, JsonElement> { [fieldKey] = budget.RootElement.Clone() }), default);
        Assert.Equal(fieldKey, values.Single().FieldKey);
        Assert.Contains((await repository.ListCustomFieldDefinitionsAsync(tenant, fixture.EntityId, "Lead", default)), item => item.Id == definition.Id);
        var pipeline = (await repository.ListPipelinesAsync(tenant, fixture.EntityId, default)).Single(x => x.Id == fixture.PipelineId);
        var reordered = await repository.ReorderStagesAsync(tenant, fixture.EntityId, fixture.PipelineId, new(pipeline.Stages.Reverse().Select(x => x.Id).ToArray(), pipeline.Version), default);
        Assert.Equal(pipeline.Stages.Count, reordered.Count);
        var requiredKey = "decision_" + Guid.CreateVersion7().ToString("N");
        await repository.CreateCustomFieldDefinitionAsync(tenant, new(fixture.EntityId, "Opportunity", requiredKey, "Decision maker", "Text", fixture.ProposalStageId), default);
        var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Required field transition", 100m, "EGP", null), default);
        await Assert.ThrowsAsync<CrmException>(() => repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(fixture.ProposalStageId, opportunity.Version), default));
        using var decision = JsonDocument.Parse("\"CFO\"");
        await repository.SetCustomFieldValuesAsync(tenant, fixture.ActorId, fixture.EntityId, new("Opportunity", opportunity.Id, new Dictionary<string, JsonElement> { [requiredKey] = decision.RootElement.Clone() }), default);
        var moved = await repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(fixture.ProposalStageId, opportunity.Version), default);
        Assert.Equal(fixture.ProposalStageId, moved.StageId);
        var snapshot = await repository.CreateForecastSnapshotAsync(tenant, fixture.ActorId, fixture.EntityId, new(DateOnly.FromDateTime(DateTime.UtcNow), "EGP"), default);
        Assert.True(snapshot.OpenOpportunityCount >= 0);
    }

    private static async Task<Fixture> FixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        const string sql = "select t.id, e.id, i.id, p.id, s1.id, s2.id, t2.id, e2.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id join platform.identity i on i.tenant_id=t.id and i.login_prefix='admin' join business_crm.crm_pipeline p on p.tenant_id=t.id and p.legal_entity_id=e.id and p.code='DEFAULT' join business_crm.crm_pipeline_stage s1 on s1.tenant_id=t.id and s1.pipeline_id=p.id and s1.sequence=1 join business_crm.crm_pipeline_stage s2 on s2.tenant_id=t.id and s2.pipeline_id=p.id and s2.sequence=3 join platform.tenant t2 on t2.id <> t.id join platform.legal_entity e2 on e2.tenant_id=t2.id order by t.id limit 1";
        await using var command = new NpgsqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(); if (!await reader.ReadAsync()) throw new InvalidOperationException("CRM seed fixture is missing."); return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7));
    }

    private sealed record Fixture(Guid TenantId, Guid EntityId, Guid ActorId, Guid PipelineId, Guid OpenStageId, Guid ProposalStageId, Guid OtherTenantId, Guid OtherEntityId);
    private sealed class CrmPostgresFactAttribute : FactAttribute { public CrmPostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TODIR_CRM_TEST_DATABASE") != "1") Skip = "Set TODIR_CRM_TEST_DATABASE=1 for the disposable PostgreSQL CRM acceptance gate."; } }
}
