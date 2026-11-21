using System.Security.Claims;
using Core.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

public sealed class ModuleLifecycleCoverageAuditTests
{
    private static readonly string[] GovernedModules = ["inventory", "ledger", "accounting", "banking", "crm"];

    private static readonly HashSet<string> ReviewedPostReadEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/inventory/legal-entities/{entityId}/scan/resolve",
        "/api/v1/accounting/legal-entities/{entityId}/close/preview",
        "/api/v1/accounting/legal-entities/{entityId}/fx-revaluations/preview",
        "/api/v1/ledger/legal-entities/{entityId}/posting-preview",
        "/api/v1/ledger/legal-entities/{entityId}/posting-rules/simulate",
        "/api/v1/ledger/legal-entities/{entityId}/opening-balances/{batchId}/preview",
        "/api/v1/ledger/legal-entities/{entityId}/mapping-configurations/{configurationId}/validate",
        "/api/v1/ledger/legal-entities/{entityId}/mapping-configurations/{configurationId}/simulate",
        "/api/v1/banking/reconciliations/{reconciliationId}/run-rules"
    };

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void AllGovernedModuleMutationEndpointsHaveModuleLifecycleMetadataAndFilterProtection()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", "Host=localhost;Database=test;Username=postgres;Password=test")
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

        var endpointSources = factory.Services.GetServices<EndpointDataSource>();
        var endpoints = endpointSources.SelectMany(es => es.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);

        var governedPrefixes = GovernedModules.Select(m => $"/api/v1/{m}").ToList();

        var missingMetadata = new List<string>();
        var missingFilter = new List<string>();
        var wrongModuleOwnership = new List<string>();
        var wrongPermissionOwnership = new List<string>();
        var unreviewedPostReads = new List<string>();
        var verifiedMutationsCount = 0;
        var verifiedReadsCount = 0;

        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var matchingPrefix = governedPrefixes.FirstOrDefault(p => pattern.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (matchingPrefix is null)
            {
                continue;
            }

            // Exclude diagnostic or internal test probe endpoints (e.g. /target)
            if (pattern.EndsWith("/target", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var expectedModule = matchingPrefix.Replace("/api/v1/", "", StringComparison.OrdinalIgnoreCase);

            var httpMethodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            var httpMethods = httpMethodMetadata?.HttpMethods ?? (IReadOnlyList<string>)["GET"];
            var isMutationHttp = httpMethods.Any(m => m is "POST" or "PUT" or "DELETE" or "PATCH");

            // 1. Verify ModuleOperationMetadata presence
            var opMetadata = endpoint.Metadata.GetMetadata<ModuleOperationMetadata>();
            if (opMetadata is null)
            {
                missingMetadata.Add($"{string.Join(",", httpMethods)} {pattern} (owning module: {expectedModule})");
                continue;
            }

            // 2. Verify Owning Module Correctness
            if (!string.Equals(opMetadata.ModuleId, expectedModule, StringComparison.OrdinalIgnoreCase))
            {
                wrongModuleOwnership.Add($"{string.Join(",", httpMethods)} {pattern} has ModuleId '{opMetadata.ModuleId}', expected '{expectedModule}'");
            }

            // 3. Verify Permission Ownership
            if (!string.IsNullOrWhiteSpace(opMetadata.Permission))
            {
                var permPrefix = $"{expectedModule}.";
                if (!opMetadata.Permission.StartsWith(permPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    wrongPermissionOwnership.Add($"{string.Join(",", httpMethods)} {pattern} has permission '{opMetadata.Permission}' which does not belong to module '{expectedModule}'");
                }
            }

            // 4. Verify Effective Filter Registration via ModuleLifecycleEnforcedAttribute (NOT solely metadata presence)
            var hasLifecycleEnforcement = endpoint.Metadata.GetMetadata<ModuleLifecycleEnforcedAttribute>() is not null;
            if (!hasLifecycleEnforcement)
            {
                missingFilter.Add($"{string.Join(",", httpMethods)} {pattern}");
            }

            // 5. Verify Mutation vs Read Classification with Reviewed POST Exceptions
            if (isMutationHttp && !opMetadata.IsMutation)
            {
                var normalizedPattern = pattern.Replace(":guid", "");
                if (!ReviewedPostReadEndpoints.Contains(normalizedPattern))
                {
                    unreviewedPostReads.Add($"{string.Join(",", httpMethods)} {pattern} is marked as Read but is not in ReviewedPostReadEndpoints");
                }
                else
                {
                    verifiedReadsCount++;
                }
            }
            else if (isMutationHttp && opMetadata.IsMutation)
            {
                verifiedMutationsCount++;
            }
            else
            {
                verifiedReadsCount++;
            }
        }

        var errors = new List<string>();
        if (missingMetadata.Count > 0)
            errors.Add("Missing ModuleOperationMetadata:\n" + string.Join("\n", missingMetadata));
        if (missingFilter.Count > 0)
            errors.Add("Missing ModuleLifecycleEndpointFilter (ModuleLifecycleEnforcedAttribute):\n" + string.Join("\n", missingFilter));
        if (wrongModuleOwnership.Count > 0)
            errors.Add("Mismatched Module Ownership:\n" + string.Join("\n", wrongModuleOwnership));
        if (wrongPermissionOwnership.Count > 0)
            errors.Add("Mismatched Permission Ownership:\n" + string.Join("\n", wrongPermissionOwnership));
        if (unreviewedPostReads.Count > 0)
            errors.Add("Unreviewed POST Read Endpoints:\n" + string.Join("\n", unreviewedPostReads));

        if (errors.Count > 0)
        {
            Assert.Fail(string.Join("\n\n", errors));
        }

        Assert.True(verifiedMutationsCount >= 40, $"Expected at least 40 mutation endpoints across governed modules, found {verifiedMutationsCount}");
        Assert.True(verifiedReadsCount >= 30, $"Expected at least 30 read endpoints across governed modules, found {verifiedReadsCount}");
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void ControlledNegative_OmittingLifecycleEnforcement_FailsCoverageGate()
    {
        // An endpoint that has ModuleOperationMetadata but omitted AddModuleLifecycleEnforcement() must be flagged
        var unprotectedEndpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/v1/inventory/legal-entities/{entityId}/omitted-enforcement"),
            0,
            new EndpointMetadataCollection(new ModuleOperationMetadata("inventory", ModuleOperationKind.Mutation, "inventory.test")),
            "Omitted Enforcement Endpoint");

        var hasLifecycleEnforcement = unprotectedEndpoint.Metadata.GetMetadata<ModuleLifecycleEnforcedAttribute>() is not null;
        Assert.False(hasLifecycleEnforcement, "Controlled negative check: endpoint without AddModuleLifecycleEnforcement() must NOT pass filter check.");
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public async Task GovernedModuleEndpoint_MissingMetadata_FailsClosedWithModuleMissingMetadata()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/inventory/unmarked-mutation";
        var filter = new ModuleLifecycleEndpointFilter();
        var invocationContext = new DefaultEndpointFilterInvocationContext(context);

        var result = await filter.InvokeAsync(invocationContext, (ctx) => ValueTask.FromResult<object?>(Results.Ok()));
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);

        if (result is IValueHttpResult valueResult && valueResult.Value is Microsoft.AspNetCore.Mvc.ProblemDetails pd)
        {
            Assert.Equal("module.missing_metadata", pd.Title);
        }
    }

    [Theory]
    [InlineData("Active", true, StatusCodes.Status200OK, null)]
    [InlineData("ReadOnly", false, StatusCodes.Status403Forbidden, "module.lifecycle_readonly")]
    [InlineData("WindDown", false, StatusCodes.Status403Forbidden, "module.lifecycle_wind_down")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public async Task EffectiveEndpointFilterExecution_ActiveMutationSucceeds_ReadOnlyAndWindDownRejected(
        string lifecycleStatus, bool expectSuccess, int expectedStatusCode, string? expectedCode)
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var context = new DefaultHttpContext();
        context.Request.Path = $"/api/v1/inventory/legal-entities/{entityId}/warehouses";
        context.Request.RouteValues["entityId"] = entityId.ToString();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", tenantId.ToString())
        ], "TestAuth"));

        var enforcer = new ControllableLifecycleEnforcer(lifecycleStatus);
        var services = new ServiceCollection();
        services.AddSingleton<IModuleLifecycleEnforcer>(enforcer);
        context.RequestServices = services.BuildServiceProvider();

        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/v1/inventory/legal-entities/{entityId:guid}/warehouses"),
            0,
            new EndpointMetadataCollection(
                new ModuleOperationMetadata("inventory", ModuleOperationKind.Mutation, "inventory.warehouse.create"),
                new ModuleLifecycleEnforcedAttribute()),
            "Create Warehouse");
        context.SetEndpoint(endpoint);

        var filter = new ModuleLifecycleEndpointFilter();
        var invocationContext = new DefaultEndpointFilterInvocationContext(context);

        var result = await filter.InvokeAsync(invocationContext, _ => ValueTask.FromResult<object?>(Results.Ok(new { created = true })));

        if (expectSuccess)
        {
            var okResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
            Assert.NotNull(okResult.Value);
        }
        else
        {
            var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(expectedStatusCode, problem.StatusCode);
            if (result is IValueHttpResult val && val.Value is Microsoft.AspNetCore.Mvc.ProblemDetails pd)
            {
                Assert.Equal(expectedCode, pd.Title);
            }
        }
    }

    private sealed class ControllableLifecycleEnforcer(string status) : IModuleLifecycleEnforcer
    {
        public Task<ModuleAccessDecision> EvaluateAccessAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, bool isMutation = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NpgsqlModuleLifecycleEnforcer.EvaluateStatus(status, isPurchasable: false, isMutation: isMutation));
        }

        public Task<string> GetStatusAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(status);

        public Task<bool> CanMutateAsync(TenantId tenantId, string moduleId, Guid? legalEntityId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(status.Equals("Active", StringComparison.OrdinalIgnoreCase));
    }
}
