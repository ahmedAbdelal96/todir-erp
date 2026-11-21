using Core.Modules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KernelIntegrationTests;

public sealed class ModuleLifecycleCoverageAuditTests
{
    private static readonly string[] GovernedModules = ["inventory", "ledger", "accounting", "banking", "crm"];

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores")]
    public void AllGovernedModuleMutationEndpointsHaveModuleLifecycleMetadata()
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
        var missingMutationMetadata = new List<string>();
        var verifiedMutationsCount = 0;
        var verifiedReadsCount = 0;

        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? string.Empty;
            if (!governedPrefixes.Any(p => pattern.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Exclude diagnostic or internal test probe endpoints (e.g. /target)
            if (pattern.EndsWith("/target", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var httpMethodMetadata = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            var httpMethods = httpMethodMetadata?.HttpMethods ?? (IReadOnlyList<string>)["GET"];
            var isMutationHttp = httpMethods.Any(m => m is "POST" or "PUT" or "DELETE" or "PATCH");

            var opMetadata = endpoint.Metadata.GetMetadata<ModuleOperationMetadata>();
            if (opMetadata is null)
            {
                missingMetadata.Add($"{string.Join(",", httpMethods)} {pattern}");
                continue;
            }

            if (isMutationHttp && !opMetadata.IsMutation)
            {
                // Verify whether this is an explicit read-only query that happens to use POST (e.g. preview, simulate, validate, scan resolve)
                var isExplicitReadOnlyPost = pattern.EndsWith("/preview", StringComparison.OrdinalIgnoreCase) ||
                                            pattern.EndsWith("/posting-preview", StringComparison.OrdinalIgnoreCase) ||
                                            pattern.EndsWith("/validate", StringComparison.OrdinalIgnoreCase) ||
                                            pattern.EndsWith("/simulate", StringComparison.OrdinalIgnoreCase) ||
                                            pattern.EndsWith("/run-rules", StringComparison.OrdinalIgnoreCase) ||
                                            pattern.EndsWith("/scan/resolve", StringComparison.OrdinalIgnoreCase);

                if (!isExplicitReadOnlyPost)
                {
                    missingMutationMetadata.Add($"{string.Join(",", httpMethods)} {pattern} (marked as {opMetadata.Kind})");
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

        if (missingMetadata.Count > 0 || missingMutationMetadata.Count > 0)
        {
            var msg = "Missing Metadata:\n" + string.Join("\n", missingMetadata) + "\n\nMissing Mutation Metadata:\n" + string.Join("\n", missingMutationMetadata);
            Assert.Fail(msg);
        }
        Assert.True(verifiedMutationsCount >= 40, $"Expected at least 40 mutation endpoints across governed modules, found {verifiedMutationsCount}");
        Assert.True(verifiedReadsCount >= 30, $"Expected at least 30 read endpoints across governed modules, found {verifiedReadsCount}");
    }
}
