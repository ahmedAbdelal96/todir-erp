using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Shared.Contracts.Platform;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PlanningAccessGovernanceAcceptanceTests : DatabaseIntegrationTestBase
{
    private static readonly Guid AtlasTenant = Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b");
    private static readonly Guid DeltaTenant = Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb");

    [Fact(DisplayName = "BC2-T10 authenticated department submissions are isolated by assignment")]
    public async Task AuthenticatedDepartmentSubmissionsAreIsolatedByAssignment()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var planner = TestIdentity.New("submission-planner", AtlasTenant);
        var otherPlanner = TestIdentity.New("submission-other", AtlasTenant);
        var permissions = new[] { "planning.versions.read", "planning.versions.edit", "planning.versions.submit" };

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, planner, permissions);
        await CreateIdentityAsync(admin, otherPlanner, permissions);

        try
        {
            var (publishedVersion, legalEntity, coordinate, department) = await PlanningSeedAsync(admin, AtlasTenant);
            using var factory = CreateFactory(runtime);
            using var plannerClient = factory.CreateClient(ClientOptions());
            using var otherPlannerClient = factory.CreateClient(ClientOptions());
            await LoginAsync(plannerClient, planner.Email, planner.Password);
            await LoginAsync(otherPlannerClient, otherPlanner.Email, otherPlanner.Password);

            using var clone = await PostWithCsrfAsync(plannerClient, $"/api/v1/planning/versions/{publishedVersion:D}/clone?name=BC2-T10-{planner.IdentityId:N}", null);
            var cloneBody = await ReadJsonAsync(clone);
            Assert.Equal(HttpStatusCode.Created, clone.StatusCode);
            var draftVersion = cloneBody.GetProperty("id").GetGuid();
            var draftCoordinate = await ScalarAsync(admin, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", AtlasTenant, draftVersion);
            Assert.NotNull(draftCoordinate);

            using var create = await PostWithCsrfAsync(plannerClient, "/api/v1/planning/submissions", new
            {
                planVersionId = draftVersion,
                legalEntityId = legalEntity,
                departmentRef = department,
                name = "BC2-T10 assigned department submission",
                assignedTo = planner.IdentityId,
                coordinateIds = new[] { (Guid)draftCoordinate! }
            });
            var created = await ReadJsonAsync(create);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var submissionId = created.GetProperty("id").GetGuid();

            using var plannerList = await plannerClient.GetAsync($"/api/v1/planning/submissions?planVersionId={draftVersion:D}");
            using var otherPlannerList = await otherPlannerClient.GetAsync($"/api/v1/planning/submissions?planVersionId={draftVersion:D}");
            Assert.Equal(HttpStatusCode.OK, plannerList.StatusCode);
            Assert.Equal(HttpStatusCode.OK, otherPlannerList.StatusCode);
            Assert.Contains(submissionId, await ReadIdsAsync(plannerList));
            Assert.DoesNotContain(submissionId, await ReadIdsAsync(otherPlannerList));
        }
        finally
        {
            await CleanupIdentityAsync(admin, planner);
            await CleanupIdentityAsync(admin, otherPlanner);
        }
    }

    [Fact(DisplayName = "BC2-T10 independent submission approval locks only attached coordinates")]
    public async Task IndependentSubmissionApprovalLocksOnlyAttachedCoordinates()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var planner = TestIdentity.New("submission-lock-planner", AtlasTenant);
        var approver = TestIdentity.New("submission-lock-approver", AtlasTenant);

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, planner, ["planning.versions.read", "planning.versions.edit", "planning.versions.submit"]);
        await CreateIdentityAsync(admin, approver, ["planning.versions.read", "planning.versions.approve"]);

        try
        {
            var (publishedVersion, legalEntity, _, department) = await PlanningSeedAsync(admin, AtlasTenant);
            using var factory = CreateFactory(runtime);
            using var plannerClient = factory.CreateClient(ClientOptions());
            using var approverClient = factory.CreateClient(ClientOptions());
            await LoginAsync(plannerClient, planner.Email, planner.Password);
            await LoginAsync(approverClient, approver.Email, approver.Password);

            using var clone = await PostWithCsrfAsync(plannerClient, $"/api/v1/planning/versions/{publishedVersion:D}/clone?name=BC2-T10-lock-{planner.IdentityId:N}", null);
            var draftVersion = (await ReadJsonAsync(clone)).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Created, clone.StatusCode);
            var seedCoordinateId = (Guid)(await ScalarAsync(admin, "select c.id from business_planning.planning_coordinate c join business_planning.planning_metric m on m.tenant_id=c.tenant_id and m.code=c.metric_code where c.tenant_id=$1 and c.plan_version_id=$2 and m.source_policy='Input' order by c.id limit 1", AtlasTenant, draftVersion) ?? throw new InvalidOperationException("A cloned writable Planning coordinate is missing."));
            var seedCoordinate = await QueryCoordinateAsync(admin, seedCoordinateId);
            var unlockedCoordinateId = Guid.CreateVersion7();
            await ExecuteAsync(admin, "insert into business_planning.planning_coordinate(tenant_id,id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,signature) select tenant_id,$2,plan_version_id,legal_entity_id,(period_start + interval '1 month')::date,metric_code,dimension_key,signature || ':bc2-t10:' || $2::text from business_planning.planning_coordinate where tenant_id=$1 and id=$3", AtlasTenant, unlockedCoordinateId, seedCoordinateId);
            var coordinateIds = new List<Guid> { seedCoordinateId, unlockedCoordinateId };

            using var create = await PostWithCsrfAsync(plannerClient, "/api/v1/planning/submissions", new
            {
                planVersionId = draftVersion,
                legalEntityId = legalEntity,
                departmentRef = department,
                name = "BC2-T10 coordinate lock",
                assignedTo = planner.IdentityId,
                coordinateIds = new[] { coordinateIds[0] }
            });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var submissionId = (await ReadJsonAsync(create)).GetProperty("id").GetGuid();

            using var submit = await PostWithCsrfAsync(plannerClient, $"/api/v1/planning/submissions/{submissionId:D}/transition", new { action = "submit", reason = "BC2-T10 submission" });
            Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
            using var approve = await PostWithCsrfAsync(approverClient, $"/api/v1/planning/submissions/{submissionId:D}/transition", new { action = "approve", reason = "Independent BC2-T10 approval" });
            Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
            Assert.Equal("Approved", (await ReadJsonAsync(approve)).GetProperty("state").GetString());

            var locked = await QueryCoordinateAsync(admin, coordinateIds[0]);
            var unlocked = await QueryCoordinateAsync(admin, coordinateIds[1]);
            using var lockedWrite = await PostWithCsrfAsync(plannerClient, "/api/v1/planning/grids/write-batch", new
            {
                planVersionId = draftVersion,
                expectedVersion = (long?)null,
                cells = new[] { new { legalEntityId = locked.LegalEntity, periodStart = locked.PeriodStart, metricCode = locked.MetricCode, dimensionKey = JsonDocument.Parse(locked.DimensionKey).RootElement, amount = 99m, currencyCode = "EGP", explicitZero = false, sourceLabel = "BC2-T10-locked" } }
            });
            Assert.True(lockedWrite.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden, await lockedWrite.Content.ReadAsStringAsync());

            using var unlockedWrite = await PostWithCsrfAsync(plannerClient, "/api/v1/planning/grids/write-batch", new
            {
                planVersionId = draftVersion,
                expectedVersion = (long?)null,
                cells = new[] { new { legalEntityId = unlocked.LegalEntity, periodStart = unlocked.PeriodStart, metricCode = unlocked.MetricCode, dimensionKey = JsonDocument.Parse(unlocked.DimensionKey).RootElement, amount = 88m, currencyCode = "EGP", explicitZero = false, sourceLabel = "BC2-T10-unlocked" } }
            });
            Assert.True(unlockedWrite.StatusCode == HttpStatusCode.OK, await unlockedWrite.Content.ReadAsStringAsync());
        }
        finally
        {
            await CleanupIdentityAsync(admin, planner);
            await CleanupIdentityAsync(admin, approver);
        }
    }

    [Fact(DisplayName = "BC2-T11 Platform Approval enforces independent approver segregation of duties")]
    public async Task PlatformApprovalEnforcesIndependentApproverSegregationOfDuties()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var requester = TestIdentity.New("approval-requester", AtlasTenant);
        var approver = TestIdentity.New("approval-approver", AtlasTenant);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, requester, ["planning.budget_control.override"]);
        await CreateIdentityAsync(admin, approver, ["planning.budget_control.override.approve"]);

        try
        {
            var (_, legalEntity, _, _) = await PlanningSeedAsync(admin, AtlasTenant);
            await using var dataSource = NpgsqlDataSource.Create(runtime);
            var approvals = new NpgsqlPlatformApprovalCapability(dataSource, TimeProvider.System);
            var request = await approvals.RequestAsync(new(AtlasTenant), requester.IdentityId, requester.MembershipId, legalEntity, "planning", "budget_override", Guid.CreateVersion7(), 1, null, "BC2-T11 SoD", "planning.budget_control.override");

            await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.DecideAsync(new(AtlasTenant), requester.IdentityId, requester.MembershipId, request.Id, "Approved", "self approval"));
            var approved = await approvals.DecideAsync(new(AtlasTenant), approver.IdentityId, approver.MembershipId, request.Id, "Approved", "independent approval");
            Assert.Equal("Approved", approved.State);
            Assert.Equal(approver.IdentityId, approved.DecidedBy);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(admin, "select count(*) from platform.approval_request where tenant_id=$1 and id=$2 and requested_by<>decided_by and state='Approved'", AtlasTenant, request.Id), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await CleanupIdentityAsync(admin, requester);
            await CleanupIdentityAsync(admin, approver);
        }
    }

    [Fact(DisplayName = "BC2-T11 delegated approval and expiry policy are available and enforced")]
    public async Task DelegatedApprovalAndExpiryPolicyAreAvailableAndEnforced()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var requester = TestIdentity.New("delegation-requester", AtlasTenant);
        var delegator = TestIdentity.New("delegation-owner", AtlasTenant);
        var delegateIdentity = TestIdentity.New("delegation-target", AtlasTenant);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, requester, ["planning.budget_control.override"]);
        await CreateIdentityAsync(admin, delegator, ["planning.budget_control.override.approve"]);
        await CreateIdentityAsync(admin, delegateIdentity, []);

        try
        {
            var (_, legalEntity, _, _) = await PlanningSeedAsync(admin, AtlasTenant);
            await InsertDelegationAsync(admin, AtlasTenant, legalEntity, delegator.IdentityId, delegateIdentity.IdentityId, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1), "BC2-T11 active delegation");
            await using var dataSource = NpgsqlDataSource.Create(runtime);
            var approvals = new NpgsqlPlatformApprovalCapability(dataSource, TimeProvider.System);

            var activeRequest = await approvals.RequestAsync(new(AtlasTenant), requester.IdentityId, requester.MembershipId, legalEntity, "planning", "budget_override", Guid.CreateVersion7(), 1, null, "BC2-T11 delegated active", "planning.budget_control.override");
            var delegatedDecision = await approvals.DecideAsync(new(AtlasTenant), delegateIdentity.IdentityId, delegateIdentity.MembershipId, activeRequest.Id, "Approved", "active delegation");
            Assert.Equal("Approved", delegatedDecision.State);
            Assert.Equal(delegateIdentity.IdentityId, delegatedDecision.DecidedBy);

            await ExecuteAsync(admin, "update platform.approval_delegation set status='Revoked' where tenant_id=$1 and delegator_id=$2 and delegate_id=$3 and effective_to > now()", AtlasTenant, delegator.IdentityId, delegateIdentity.IdentityId);
            await InsertDelegationAsync(admin, AtlasTenant, legalEntity, delegator.IdentityId, delegateIdentity.IdentityId, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1), "BC2-T11 expired delegation");
            var expiredRequest = await approvals.RequestAsync(new(AtlasTenant), requester.IdentityId, requester.MembershipId, legalEntity, "planning", "budget_override", Guid.CreateVersion7(), 1, null, "BC2-T11 delegated expired", "planning.budget_control.override");
            await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.DecideAsync(new(AtlasTenant), delegateIdentity.IdentityId, delegateIdentity.MembershipId, expiredRequest.Id, "Approved", "expired delegation"));
            Assert.Equal("Pending", (await approvals.GetAsync(new(AtlasTenant), expiredRequest.Id))!.State);
        }
        finally
        {
            await ExecuteAsync(admin, "delete from platform.approval_delegation where tenant_id=$1 and (delegator_id=$2 or delegate_id=$3)", AtlasTenant, delegator.IdentityId, delegateIdentity.IdentityId);
            await CleanupIdentityAsync(admin, requester);
            await CleanupIdentityAsync(admin, delegator);
            await CleanupIdentityAsync(admin, delegateIdentity);
        }
    }

    [Fact(DisplayName = "BC2-T14 workforce resource confidentiality is enforced across permission and tenant boundaries")]
    public async Task WorkforceResourceConfidentialityIsEnforcedAcrossPermissionAndTenantBoundaries()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var planner = TestIdentity.New("workforce-planner", AtlasTenant);
        var sensitive = TestIdentity.New("workforce-sensitive", AtlasTenant);
        var deltaReader = TestIdentity.New("workforce-delta", DeltaTenant);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, planner, ["planning.workforce.read", "planning.versions.read"]);
        await CreateIdentityAsync(admin, sensitive, ["planning.workforce.read", "planning.workforce.sensitive.read", "planning.versions.read"]);
        await CreateIdentityAsync(admin, deltaReader, ["planning.workforce.read", "planning.versions.read"]);

        try
        {
            var version = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version v where v.tenant_id=$1 and v.state='Published' and exists(select 1 from business_planning.workforce_plan_line w where w.tenant_id=v.tenant_id and w.plan_version_id=v.id) order by v.created_at desc limit 1", AtlasTenant))!;
            using var factory = CreateFactory(runtime);
            using var plannerClient = factory.CreateClient(ClientOptions());
            using var sensitiveClient = factory.CreateClient(ClientOptions());
            using var deltaClient = factory.CreateClient(ClientOptions());
            await LoginAsync(plannerClient, planner.Email, planner.Password);
            await LoginAsync(sensitiveClient, sensitive.Email, sensitive.Password);
            await LoginAsync(deltaClient, deltaReader.Email, deltaReader.Password);

            using var publicResponse = await plannerClient.GetAsync($"/api/v1/planning/workforce?planVersionId={version:D}");
            using var sensitiveResponse = await sensitiveClient.GetAsync($"/api/v1/planning/workforce?planVersionId={version:D}");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, sensitiveResponse.StatusCode);
            var publicRows = await ReadJsonArrayAsync(publicResponse);
            var sensitiveRows = await ReadJsonArrayAsync(sensitiveResponse);
            Assert.True(publicRows.GetArrayLength() > 0);
            Assert.Equal(publicRows.GetArrayLength(), sensitiveRows.GetArrayLength());
            Assert.All(publicRows.EnumerateArray(), row =>
            {
                Assert.True(row.GetProperty("roleCode").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
                Assert.Equal(0m, row.GetProperty("averageCost").GetDecimal());
            });
            Assert.Contains(sensitiveRows.EnumerateArray().ToArray(), row => row.GetProperty("averageCost").GetDecimal() > 0m);

            using var crossTenant = await deltaClient.GetAsync($"/api/v1/planning/workforce?planVersionId={version:D}");
            Assert.True(crossTenant.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, await crossTenant.Content.ReadAsStringAsync());
            Assert.DoesNotContain(version.ToString("D"), await crossTenant.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupIdentityAsync(admin, planner);
            await CleanupIdentityAsync(admin, sensitive);
            await CleanupIdentityAsync(admin, deltaReader);
        }
    }

    [Fact(DisplayName = "BC2-T17 disabled ReadOnly and not-entitled owner modules fail closed for Planning workflows")]
    public async Task DisabledReadOnlyAndNotEntitledOwnerModulesFailClosed()
    {
        var runtime = Required("TODIR_DATABASE_CONNECTION");
        var adminConnection = Required("TODIR_P04_ADMIN_CONNECTION");
        var actor = TestIdentity.New("owner-compatibility", AtlasTenant);
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await CreateIdentityAsync(admin, actor, ["planning.actuals.refresh", "planning.versions.read", "planning.versions.edit"]);
        var version = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version v where v.tenant_id=$1 and v.state='Published' and exists(select 1 from business_planning.planning_coordinate c where c.tenant_id=v.tenant_id and c.plan_version_id=v.id) order by v.created_at desc limit 1", AtlasTenant))!;
        var entity = (Guid)(await ScalarAsync(admin, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", AtlasTenant, version))!;
        var originalPlanningStatus = await ScalarAsync(admin, "select status from platform.module_instance where tenant_id=$1 and module_id='planning' and scope_type='tenant'", AtlasTenant) as string;
        var originalPayrollStatus = await ScalarAsync(admin, "select status from platform.module_instance where tenant_id=$1 and module_id='payroll' and scope_type='tenant'", AtlasTenant) as string;
        var originalPurchasingStatus = await ScalarAsync(admin, "select status from platform.module_instance where tenant_id=$1 and module_id='purchasing' and scope_type='tenant'", AtlasTenant) as string;
        var originalEntitlements = (await ScalarAsync(admin, "select modules::text from platform.entitlement_snapshot where tenant_id=$1 order by snapshot_version desc limit 1", AtlasTenant))?.ToString();

        try
        {
            using var factory = CreateFactory(runtime);
            using var adminClient = factory.CreateClient(ClientOptions());
            await LoginAsync(adminClient, actor.Email, actor.Password);
            var failures = new List<string>();

            await SetModuleStatusAsync(admin, AtlasTenant, "payroll", "Disabled");
            using var disabledOwner = await PostWithCsrfAsync(adminClient, "/api/v1/planning/actuals/refresh", new { mappingVersion = "seed-v1", sourceWatermark = $"BC2-T17-disabled-{Guid.CreateVersion7():N}", planVersionId = version, actualThrough = "2027-12-01" });
            if (disabledOwner.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)) failures.Add($"disabled owner returned {(int)disabledOwner.StatusCode} {disabledOwner.StatusCode}");

            await SetModuleStatusAsync(admin, AtlasTenant, "planning", "ReadOnly");
            using var readOnlyMutation = await PostWithCsrfAsync(adminClient, "/api/v1/planning/workforce", new { planVersionId = version, legalEntityId = entity, departmentRef = (Guid?)null, roleCode = "BC2-T17", periodStart = "2027-12-01", fte = 1m, averageCost = 1m, currencyCode = "EGP", source = "BC2-T17" });
            var readOnlyBody = await readOnlyMutation.Content.ReadAsStringAsync();
            if (readOnlyMutation.StatusCode != HttpStatusCode.Forbidden || !readOnlyBody.Contains("module.lifecycle_readonly", StringComparison.OrdinalIgnoreCase)) failures.Add($"ReadOnly owner returned {(int)readOnlyMutation.StatusCode} {readOnlyMutation.StatusCode}");

            await SetModuleStatusAsync(admin, AtlasTenant, "planning", originalPlanningStatus ?? "Active");
            await ExecuteAsync(admin, "update platform.entitlement_snapshot set modules=modules-'payroll' where tenant_id=$1", AtlasTenant);
            using var notEntitledOwner = await PostWithCsrfAsync(adminClient, "/api/v1/planning/actuals/refresh", new { mappingVersion = "seed-v1", sourceWatermark = $"BC2-T17-not-entitled-{Guid.CreateVersion7():N}", planVersionId = version, actualThrough = "2027-12-01" });
            if (notEntitledOwner.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)) failures.Add($"not-entitled owner returned {(int)notEntitledOwner.StatusCode} {notEntitledOwner.StatusCode}");
            Assert.Empty(failures);
        }
        finally
        {
            if (originalPlanningStatus is not null) await SetModuleStatusAsync(admin, AtlasTenant, "planning", originalPlanningStatus);
            if (originalPayrollStatus is not null) await SetModuleStatusAsync(admin, AtlasTenant, "payroll", originalPayrollStatus);
            if (originalPurchasingStatus is not null) await SetModuleStatusAsync(admin, AtlasTenant, "purchasing", originalPurchasingStatus);
            else await ExecuteAsync(admin, "delete from platform.module_instance where tenant_id=$1 and module_id='purchasing' and scope_type='tenant'", AtlasTenant);
            if (originalEntitlements is not null) await ExecuteAsync(admin, "update platform.entitlement_snapshot set modules=$2::jsonb where tenant_id=$1", AtlasTenant, originalEntitlements);
            await CleanupIdentityAsync(admin, actor);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string runtime) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("TODIR_DATABASE_CONNECTION", runtime)
        .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

    private static WebApplicationFactoryClientOptions ClientOptions() => new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true };

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object? body)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadJsonArrayAsync(HttpResponseMessage response)
    {
        var json = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        return json;
    }

    private static async Task<HashSet<Guid>> ReadIdsAsync(HttpResponseMessage response)
    {
        var json = await ReadJsonArrayAsync(response);
        return json.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToHashSet();
    }

    private static async Task<(Guid Version, Guid LegalEntity, Guid Coordinate, Guid Department)> PlanningSeedAsync(NpgsqlConnection connection, Guid tenant)
    {
        var version = (Guid)(await ScalarAsync(connection, "select id from business_planning.plan_version v where v.tenant_id=$1 and v.state='Published' and exists(select 1 from business_planning.planning_coordinate c where c.tenant_id=v.tenant_id and c.plan_version_id=v.id) order by v.created_at desc limit 1", tenant) ?? throw new InvalidOperationException("Published Planning version is missing."));
        var entity = (Guid)(await ScalarAsync(connection, "select legal_entity_id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 order by id limit 1", tenant, version) ?? throw new InvalidOperationException("Planning coordinate is missing."));
        var coordinate = (Guid)(await ScalarAsync(connection, "select id from business_planning.planning_coordinate where tenant_id=$1 and plan_version_id=$2 and legal_entity_id=$3 order by id limit 1", tenant, version, entity) ?? throw new InvalidOperationException("Planning coordinate is missing."));
        var department = (Guid)(await ScalarAsync(connection, "select id from business_hr.department where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by code limit 1", tenant, entity) ?? throw new InvalidOperationException("Active HR department is missing."));
        return (version, entity, coordinate, department);
    }

    private static async Task<List<Guid>> QueryGuidsAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = CreateCommand(connection, sql, values);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<Guid>();
        while (await reader.ReadAsync()) result.Add(reader.GetGuid(0));
        return result;
    }

    private static async Task<(Guid LegalEntity, DateOnly PeriodStart, string MetricCode, string DimensionKey)> QueryCoordinateAsync(NpgsqlConnection connection, Guid coordinateId)
    {
        await using var command = CreateCommand(connection, "select legal_entity_id,period_start,metric_code,dimension_key::text from business_planning.planning_coordinate where id=$1", coordinateId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetString(3));
    }

    private static async Task CreateIdentityAsync(NpgsqlConnection connection, TestIdentity identity, IReadOnlyList<string> permissions)
    {
        var hash = IdentityConventions.HashPassword(identity.Password);
        await using (var command = CreateCommand(connection, "set role erp_owner; select set_config('app.tenant_id',$1::text,false); insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,status,created_at,security_stamp,version) values($2,$1,$3,$4,$5,$6,true,$7,'Active',now(),$8,1); insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($9,$1,$2,$6,'Active',now(),1); insert into platform.role(id,tenant_id,code,name,is_system_template,is_active,version) values($10,$1,$5,$6,false,true,1); insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type) values($1,$9,$10,'Tenant');", identity.TenantId, identity.IdentityId, identity.Email, identity.Email.ToUpperInvariant(), identity.LoginPrefix, identity.Email, hash, Guid.CreateVersion7(), identity.MembershipId, identity.RoleId))
            await command.ExecuteNonQueryAsync();
        foreach (var permission in permissions)
            await ExecuteAsync(connection, "insert into platform.role_permission(tenant_id,role_id,permission_code) values($1,$2,$3)", identity.TenantId, identity.RoleId, permission);
        await ExecuteAsync(connection, "reset app.tenant_id; reset role;");
    }

    private static async Task CleanupIdentityAsync(NpgsqlConnection connection, TestIdentity identity)
    {
        await ExecuteAsync(connection, "set role erp_owner; select set_config('app.tenant_id',$1::text,false); delete from platform.membership_role_assignment where tenant_id=$1 and membership_id=$2; delete from platform.role_permission where tenant_id=$1 and role_id=$3; delete from platform.role where tenant_id=$1 and id=$3; delete from platform.membership where tenant_id=$1 and id=$2; delete from platform.identity where tenant_id=$1 and id=$4; reset app.tenant_id; reset role;", identity.TenantId, identity.MembershipId, identity.RoleId, identity.IdentityId);
    }

    private static async Task SetModuleStatusAsync(NpgsqlConnection connection, Guid tenant, string moduleId, string status)
    {
        await ExecuteAsync(connection, "update platform.module_instance set status=$3 where tenant_id=$1 and module_id=$2 and scope_type='tenant'", tenant, moduleId, status);
    }

    private static Task<int> InsertDelegationAsync(NpgsqlConnection connection, Guid tenant, Guid legalEntity, Guid delegator, Guid delegateId, DateTimeOffset effectiveFrom, DateTimeOffset effectiveTo, string reason) =>
        ExecuteAsync(connection, "insert into platform.approval_delegation(tenant_id,id,legal_entity_id,delegator_id,delegate_id,permission_code,effective_from,effective_to,status,reason,created_by,created_at) values($1,$2,$3,$4,$5,'planning.budget_control.override.approve',$6,$7,'Active',$8,$4,now())", tenant, Guid.CreateVersion7(), legalEntity, delegator, delegateId, effectiveFrom, effectiveTo, reason);

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = CreateCommand(connection, sql, values);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<int> ExecuteAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = CreateCommand(connection, sql, values);
        return await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, params object[] values)
    {
        var command = new NpgsqlCommand(sql, connection);
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue($"p{i + 1}", values[i]);
        command.CommandText = sql;
        for (var i = values.Length; i >= 1; i--) command.CommandText = command.CommandText.Replace($"${i}", $"@p{i}", StringComparison.Ordinal);
        return command;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");

    private sealed record TestIdentity(Guid TenantId, Guid IdentityId, Guid MembershipId, Guid RoleId, string Email, string Password, string LoginPrefix)
    {
        public static TestIdentity New(string prefix, Guid tenant)
        {
            var id = Guid.CreateVersion7();
            return new(tenant, id, Guid.CreateVersion7(), Guid.CreateVersion7(), $"{prefix}-{id:N}@{(tenant == AtlasTenant ? "atlas" : "delta")}.com", $"Aa1!{id:N}z", $"test-{id:N}");
        }
    }
}
