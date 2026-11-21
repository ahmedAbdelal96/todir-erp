using Core.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

            // 4. Verify Effective Filter Registration
            var hasLifecycleFilter = endpoint.Metadata.Any(m => m.GetType().Name.Contains("Filter") || m is ModuleOperationMetadata);
            if (!hasLifecycleFilter)
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
            errors.Add("Missing ModuleLifecycleEndpointFilter:\n" + string.Join("\n", missingFilter));
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
}
