using System.Text;
using System.Globalization;
using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets;
using Modules.FixedAssets.Services;
using Shared.Contracts.FixedAssets;
using Shared.Primitives.Identifiers;

namespace Modules.FixedAssets.Controllers;

public static class FixedAssetsModuleExtensions
{
    public static IServiceCollection AddFixedAssetsModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, FixedAssetsModuleManifestProvider>();
        services.AddSingleton<FixedAssetRepository>();
        services.AddSingleton<IFixedAssetCustodyCapabilities>(provider => provider.GetRequiredService<FixedAssetRepository>());
        services.AddSingleton<EgyptFixedAssetCountryPolicyProvider>();
        services.AddSingleton<IFixedAssetCountryPolicyProvider>(provider => provider.GetRequiredService<EgyptFixedAssetCountryPolicyProvider>());
        services.AddSingleton<IFixedAssetRevaluationProvider>(provider => provider.GetRequiredService<EgyptFixedAssetCountryPolicyProvider>());
        services.AddSingleton<FixedAssetService>();
        return services;
    }

    public static WebApplication MapFixedAssetsModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/fixed-assets").WithTags("Fixed Assets").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/categories", async (int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.read", ct), access => service.ListCategoriesAsync(access.Tenant, limit ?? 100, ct)));
        group.MapPost("/categories", async (CreateAssetCategoryRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await TenantAccessAsync(http, subjects, authorizer, "assets.book.manage", ct), access => service.CreateCategoryAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapGet("/locations", async (Guid? legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.ListLocationsAsync(access.Tenant, legalEntityId, ct)));
        group.MapPost("/locations", async (CreateAssetLocationRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.book.manage", request.LegalEntityId, ct), access => service.CreateLocationAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapGet("/verification/campaigns", async (Guid? legalEntityId, string? status, int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.ListVerificationCampaignsAsync(access.Tenant, legalEntityId, status, limit ?? 100, ct)));
        group.MapPost("/verification/campaigns", async (CreateVerificationCampaignRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.verification.manage", request.LegalEntityId, ct), access => service.CreateVerificationCampaignAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/verification/campaigns/{campaignId:guid}/items/{assetId:guid}/verify", async (Guid campaignId, Guid assetId, VerifyAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.verification.manage", "campaign", campaignId, ct), access => service.VerifyAssetAsync(access.Tenant, access.ActorId, campaignId, assetId, request, ct)));
        group.MapPost("/verification/campaigns/{campaignId:guid}/close", async (Guid campaignId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.verification.manage", "campaign", campaignId, ct), access => service.CloseVerificationCampaignAsync(access.Tenant, access.ActorId, campaignId, ct)));
        group.MapGet("/assets", async (Guid? legalEntityId, string? state, string? search, string? nature, Guid? locationId, int? limit, int? offset, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.ListAssetsAsync(access.Tenant, legalEntityId, state, search, nature, locationId, limit ?? 50, offset ?? 0, ct)));
        group.MapGet("/reports/register", async (Guid legalEntityId, DateOnly? asOfDate, Guid? bookId, string? state, string? search, int? limit, int? offset, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", legalEntityId, ct), async access => new { LegalEntityId = legalEntityId, AsOfDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), Items = await service.ListAssetRegisterAsync(access.Tenant, legalEntityId, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), bookId, state, search, Math.Clamp(limit ?? 100, 1, 200), Math.Max(offset ?? 0, 0), ct) }));
        group.MapGet("/reports/register/export", async (Guid legalEntityId, DateOnly? asOfDate, Guid? bookId, string? state, string? search, int? batchSize, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var check = await EntityAccessAsync(http, service, subjects, authorizer, "assets.export", legalEntityId, ct);
            if (check.Outcome == AccessOutcome.Unauthorized) return Results.Unauthorized();
            if (check.Outcome == AccessOutcome.Forbidden) return Results.Forbid();
            var access = check.Access!;
            var asOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var effectiveBatchSize = Math.Clamp(batchSize ?? 200, 5, 500);
            var csv = new StringBuilder("AssetNumber,Name,State,Nature,Quantity,BookCode,GrossCost,AccumulatedDepreciation,AccumulatedImpairment,Revaluation,NetBookValue,Custodian\n");
            for (var offset = 0; ; offset += effectiveBatchSize)
            {
                var rows = await service.ListAssetRegisterAsync(access.Tenant, legalEntityId, asOf, bookId, state, search, effectiveBatchSize, offset, ct);
                foreach (var row in rows)
                {
                    csv.AppendLine(string.Join(",", new object?[] {
                        row.AssetNumber,
                        row.Name,
                        row.LifecycleState,
                        row.AssetNature,
                        row.Quantity.ToString(CultureInfo.InvariantCulture),
                        row.BookCode ?? string.Empty,
                        row.GrossCost.ToString(CultureInfo.InvariantCulture),
                        row.AccumulatedDepreciation.ToString(CultureInfo.InvariantCulture),
                        row.AccumulatedImpairment.ToString(CultureInfo.InvariantCulture),
                        row.RevaluationEffects.ToString(CultureInfo.InvariantCulture),
                        row.NetBookValue.ToString(CultureInfo.InvariantCulture),
                        row.CustodianName ?? string.Empty
                    }.Select(EscapeCsv)));
                }
                if (rows.Count < effectiveBatchSize) break;
            }
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", "fixed-asset-register.csv");
        });
        group.MapGet("/country-policy/book-defaults", async (Guid legalEntityId, string bookCode, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", legalEntityId, ct), access => service.GetCountryBookDefaultsAsync(access.Tenant, legalEntityId, bookCode, ct)));
        group.MapPost("/mass-transfers/preview", async (MassTransferRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.assign", request.LegalEntityId, ct), access => service.PreviewMassTransferAsync(access.Tenant, request, ct)));
        group.MapPost("/mass-transfers", async (MassTransferRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.assign", request.LegalEntityId, ct), access => service.ExecuteMassTransferAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapGet("/assets/{assetId:guid}/timeline", async (Guid assetId, int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.audit.read", "asset", assetId, ct), access => service.GetAssetTimelineAsync(access.Tenant, assetId, limit ?? 100, ct)));
        group.MapGet("/assets/{assetId:guid}/source-drilldown", async (Guid assetId, Guid? bookId, int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", "asset", assetId, ct), access => service.ListAssetSourceDrilldownAsync(access.Tenant, assetId, bookId, limit ?? 100, ct)));
        group.MapPost("/assets", async (CreateAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.create", request.LegalEntityId, ct), access => service.CreateAssetAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/assignments", async (Guid assetId, AssignAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.assign", "asset", assetId, ct), access => service.AssignAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/transfer", async (Guid assetId, AssignAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.assign", "asset", assetId, ct), access => service.TransferAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/return", async (Guid assetId, ReturnAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.assign", "asset", assetId, ct), access => service.ReturnAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapGet("/assets/{assetId:guid}/assignments", async (Guid assetId, DateOnly? asOfDate, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.read", "asset", assetId, ct), access => service.ListAssignmentsAsync(access.Tenant, assetId, asOfDate, ct)));
        group.MapGet("/assets/{assetId:guid}/scan", async (Guid assetId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.read", "asset", assetId, ct), access => service.GetAssetScanAsync(access.Tenant, null, assetId, null, ct)));
        group.MapGet("/assets/scan", async (Guid legalEntityId, string assetTag, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.GetAssetScanAsync(access.Tenant, legalEntityId, null, assetTag, ct)));
        group.MapGet("/employees/{employeeId:guid}/assets", async (Guid employeeId, Guid legalEntityId, DateOnly? asOfDate, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.assign", legalEntityId, ct), access => service.ListEmployeeAssetsAsync(access.Tenant, employeeId, legalEntityId, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct)));
        group.MapGet("/my-assets", async (Guid legalEntityId, DateOnly? asOfDate, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.ListMyAssetsAsync(access.Tenant, access.MembershipId, legalEntityId, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct)));
        group.MapGet("/assets/{assetId:guid}/documents", async (Guid assetId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.documents.read", "asset", assetId, ct), access => service.ListAssetDocumentsAsync(access.Tenant, assetId, ct)));
        group.MapPost("/assets/{assetId:guid}/documents", async (Guid assetId, AddAssetDocumentRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.documents.manage", "asset", assetId, ct), access => service.AddAssetDocumentAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/handovers", async (Guid assetId, CreateAssetHandoverRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.assign", "asset", assetId, ct), access => service.CreateHandoverAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapGet("/books", async (Guid? legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.financial.read", legalEntityId, ct), access => service.ListBooksAsync(access.Tenant, legalEntityId, ct)));
        group.MapPost("/books", async (CreateBookRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.book.manage", request.LegalEntityId, ct), access => service.CreateBookAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/books", async (Guid assetId, CreateAssetBookRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.book.manage", "asset", assetId, ct), access => service.AddAssetBookAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/depreciation-runs/prepare", async (CreateDepreciationRunRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.depreciation.prepare", request.LegalEntityId, ct), access => service.PrepareDepreciationRunAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/depreciation-runs/{runId:guid}/post", async (Guid runId, Guid legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.depreciation.post", "run", runId, ct), access => service.PostDepreciationRunAsync(access.Tenant, access.ActorId, runId, ct)));
        group.MapPost("/depreciation-runs/{runId:guid}/reverse", async (Guid runId, Guid legalEntityId, ReverseDepreciationRunRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.depreciation.post", "run", runId, ct), access => service.ReverseDepreciationRunAsync(access.Tenant, access.ActorId, runId, request, ct)));
        group.MapGet("/depreciation-runs", async (Guid? legalEntityId, int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.financial.read", legalEntityId, ct), access => service.ListDepreciationRunsAsync(access.Tenant, legalEntityId, limit ?? 100, ct)));
        group.MapGet("/capitalization-candidates", async (Guid? legalEntityId, string? status, int? limit, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ListAccessAsync(http, service, subjects, authorizer, "assets.read", legalEntityId, ct), access => service.ListCapitalizationCandidatesAsync(access.Tenant, legalEntityId, status, limit ?? 100, ct)));
        group.MapPost("/capitalization-candidates", async (CreateCapitalizationCandidateRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCapitalizationCandidateAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/capitalization-candidates/from-invoice", async (CreateCapitalizationCandidateFromInvoiceRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCapitalizationCandidateFromInvoiceAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/capitalization-candidates/from-inventory", async (CreateCapitalizationCandidateFromInventoryRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCapitalizationCandidateFromInventoryAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/capitalization-candidates/from-expense", async (CreateCapitalizationCandidateFromExpenseRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCapitalizationCandidateFromExpenseAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/capitalization-candidates/from-purchasing", async (CreateCapitalizationCandidateFromPurchasingRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCapitalizationCandidateFromPurchasingAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/capitalization-candidates/{candidateId:guid}/review", async (Guid candidateId, ReviewCapitalizationCandidateRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "candidate", candidateId, ct), access => service.ReviewCapitalizationCandidateAsync(access.Tenant, access.ActorId, candidateId, request, ct)));
        group.MapPost("/capitalization-candidates/{candidateId:guid}/allocations", async (Guid candidateId, AllocateCapitalizationCandidateRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "candidate", candidateId, ct), access => service.AllocateCapitalizationCandidateAsync(access.Tenant, access.ActorId, candidateId, request, ct)));
        group.MapPost("/capitalization-candidates/{candidateId:guid}/allocations/{allocationId:guid}/correct", async (Guid candidateId, Guid allocationId, CorrectSourceAllocationRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "candidate", candidateId, ct), access => service.CorrectSourceAllocationAsync(access.Tenant, access.ActorId, candidateId, request with { AllocationId = allocationId }, ct)));
        group.MapPost("/cip-assets", async (CreateCipAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CreateCipAssetAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/cip-assets/{assetId:guid}/cost-sources", async (Guid assetId, AddCipCostSourceRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "asset", assetId, ct), access => service.AddCipCostSourceAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/cip-assets/{assetId:guid}/capitalize", async (Guid assetId, CapitalizeCipRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "asset", assetId, ct), access => service.CapitalizeCipAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/cip-assets/{assetId:guid}/split", async (Guid assetId, SplitCipRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", "asset", assetId, ct), access => service.SplitCipAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/split-grouped", async (Guid assetId, SplitGroupedAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.edit", "asset", assetId, ct), access => service.SplitGroupedAssetAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/cip-assets/combine", async (CombineCipRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.capitalize", request.LegalEntityId, ct), access => service.CombineCipAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/cost-change", async (Guid assetId, Guid bookId, ChangeAssetBookCostRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.book.manage", "asset", assetId, ct), access => service.ChangeAssetBookCostAsync(access.Tenant, access.ActorId, assetId, request with { BookId = bookId }, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/estimate-change", async (Guid assetId, Guid bookId, CreateAssetBookEstimateChangeRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.book.manage", "asset", assetId, ct), access => service.CreateAssetBookEstimateChangeAsync(access.Tenant, access.ActorId, assetId, bookId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/impairments", async (Guid assetId, Guid bookId, CreateAssetImpairmentRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.impair", "asset", assetId, ct), access => service.CreateAssetImpairmentAsync(access.Tenant, access.ActorId, assetId, request with { BookId = bookId }, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/impairment-approval-requests", async (Guid assetId, Guid bookId, RequestAssetImpairmentApprovalRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.impair", "asset", assetId, ct), access => service.RequestAssetImpairmentApprovalAsync(access.Tenant, access.ActorId, access.MembershipId, assetId, request with { BookId = bookId }, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/revaluations", async (Guid assetId, Guid bookId, CreateAssetRevaluationRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.revalue", "asset", assetId, ct), access => service.ProposeAssetRevaluationAsync(access.Tenant, access.ActorId, assetId, request with { BookId = bookId }, ct)));
        group.MapPost("/assets/{assetId:guid}/books/{bookId:guid}/revaluation-approval-requests", async (Guid assetId, Guid bookId, RequestAssetRevaluationApprovalRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.revalue", "asset", assetId, ct), access => service.RequestAssetRevaluationApprovalAsync(access.Tenant, access.ActorId, access.MembershipId, assetId, request with { BookId = bookId }, ct)));
        group.MapPost("/opening-imports", async (CreateOpeningImportRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await EntityAccessAsync(http, service, subjects, authorizer, "assets.opening.manage", request.LegalEntityId, ct), access => service.CreateOpeningImportAsync(access.Tenant, access.ActorId, request, ct)));
        group.MapPost("/opening-imports/{importId:guid}/post", async (Guid importId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.opening.manage", "opening", importId, ct), access => service.PostOpeningImportAsync(access.Tenant, access.ActorId, importId, ct)));
        group.MapGet("/assets/{assetId:guid}/books/{bookId:guid}/balance", async (Guid assetId, Guid bookId, Guid legalEntityId, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", "book", bookId, ct), access => service.GetAssetBookBalanceAsync(access.Tenant, assetId, bookId, legalEntityId, ct)));
        group.MapGet("/assets/{assetId:guid}/books/{bookId:guid}/schedule", async (Guid assetId, Guid bookId, Guid legalEntityId, DateOnly from, DateOnly to, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", "book", bookId, ct), access => service.GetDepreciationScheduleAsync(access.Tenant, assetId, bookId, legalEntityId, from, to, ct)));
        group.MapGet("/books/{bookId:guid}/reconciliation", async (Guid bookId, Guid legalEntityId, DateOnly? asOfDate, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.financial.read", "book-definition", bookId, ct), access => service.ReconcileAssetBookAsync(access.Tenant, bookId, legalEntityId, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct)));
        group.MapPost("/assets/{assetId:guid}/disposal", async (Guid assetId, DisposeAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.disposal.manage", "asset", assetId, ct), access => service.DisposeAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/sell", async (Guid assetId, SellAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.disposal.manage", "asset", assetId, ct), access => service.SellAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        group.MapPost("/assets/{assetId:guid}/reinstate", async (Guid assetId, ReinstateAssetRequest request, HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) => await Execute(http, await ResourceEntityAccessAsync(http, service, subjects, authorizer, "assets.disposal.manage", "asset", assetId, ct), access => service.ReinstateAsync(access.Tenant, access.ActorId, assetId, request, ct)));
        return app;
    }

    private enum AccessOutcome { Granted, Unauthorized, Forbidden }
    private sealed record AccessCheck(AccessOutcome Outcome, Access? Access)
    {
        public static AccessCheck Granted(Access access) => new(AccessOutcome.Granted, access);
        public static AccessCheck Unauthorized() => new(AccessOutcome.Unauthorized, null);
        public static AccessCheck Forbidden() => new(AccessOutcome.Forbidden, null);
    }

    private static async Task<IResult> Execute<T>(HttpContext http, AccessCheck check, Func<Access, Task<T>> action)
    {
        if (check.Outcome == AccessOutcome.Unauthorized) return Results.Unauthorized();
        if (check.Outcome == AccessOutcome.Forbidden) return Results.Forbid();
        try { return Results.Ok(await action(check.Access!)); }
        catch (FixedAssetException exception) { return Results.Problem(statusCode: 400, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code }); }
    }

    private static string EscapeCsv(object? value) { var text = value?.ToString() ?? string.Empty; return text.Contains(',') || text.Contains('"') || text.Contains('\n') ? $"\"{text.Replace("\"", "\"\"")}\"" : text; }

    private static async Task<AccessCheck> TenantAccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, CancellationToken ct)
    {
        if (!TryIdentity(http, out var tenantId, out var membershipId, out var actorId)) return AccessCheck.Unauthorized();
        var tenant = new TenantId(tenantId); var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return AccessCheck.Unauthorized();
        return authorizer.Authorize(subject, permission, PermissionScope.ForTenant(tenant))
            ? AccessCheck.Granted(new(tenant, actorId, membershipId))
            : AccessCheck.Forbidden();
    }

    private static Task<AccessCheck> ListAccessAsync(HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid? legalEntityId, CancellationToken ct) =>
        legalEntityId is { } entityId
            ? EntityAccessAsync(http, service, subjects, authorizer, permission, entityId, ct)
            : TenantAccessAsync(http, subjects, authorizer, permission, ct);

    private static async Task<AccessCheck> ResourceEntityAccessAsync(HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, string resourceType, Guid resourceId, CancellationToken ct)
    {
        if (!TryIdentity(http, out var tenantId, out var membershipId, out var actorId)) return AccessCheck.Unauthorized();
        var tenant = new TenantId(tenantId);
        var legalEntityId = await service.ResolveLegalEntityAsync(tenant, resourceType, resourceId, ct);
        if (legalEntityId is null) return AccessCheck.Forbidden();
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return AccessCheck.Unauthorized();
        return authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(legalEntityId.Value)))
            ? AccessCheck.Granted(new(tenant, actorId, membershipId))
            : AccessCheck.Forbidden();
    }

    private static async Task<AccessCheck> EntityAccessAsync(HttpContext http, FixedAssetService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        if (!TryIdentity(http, out var tenantId, out var membershipId, out var actorId)) return AccessCheck.Unauthorized();
        var tenant = new TenantId(tenantId);
        if (!await service.LegalEntityExistsAsync(tenant, entityId, ct)) return AccessCheck.Forbidden();
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return AccessCheck.Unauthorized();
        return authorizer.Authorize(subject, permission, PermissionScope.ForLegalEntity(tenant, new LegalEntityId(entityId)))
            ? AccessCheck.Granted(new(tenant, actorId, membershipId))
            : AccessCheck.Forbidden();
    }

    private static bool TryIdentity(HttpContext http, out Guid tenantId, out Guid membershipId, out Guid actorId)
    {
        var tenantOk = Guid.TryParse(http.User.FindFirstValue("tenant_id"), out tenantId);
        var membershipOk = Guid.TryParse(http.User.FindFirstValue("membership_id"), out membershipId);
        var actorValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        var actorOk = Guid.TryParse(actorValue, out actorId);
        return tenantOk && membershipOk && actorOk;
    }
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId);
}

public sealed class FixedAssetsModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "fixed-assets", "1.0.0", 1, "L4", "business-module", true, "tenant",
        new(["platform-kernel", "finbase", "ledger"], ["catalog.product", "inventory.history", "hr.core", "platform.files", "platform.approvals", "platform.outbox"]),
        new("business_assets", ["asset_category", "asset_category_version", "fixed_asset", "asset_location", "asset_assignment", "asset_handover", "asset_document_reference", "asset_book_definition", "asset_book", "asset_book_transaction", "asset_book_estimate_change", "asset_depreciation_run", "asset_depreciation_run_line", "asset_capitalization_candidate", "asset_source_allocation", "asset_cip_cost_source", "asset_cip_split_allocation", "asset_mass_transfer", "asset_mass_transfer_item", "asset_verification_campaign", "asset_verification_line", "asset_disposal", "asset_sale", "asset_disposal_reinstatement", "asset_impairment", "asset_revaluation", "asset_opening_import", "asset_opening_import_line", "asset_audit_event", "asset_idempotency_key"]),
        ["assets.read", "assets.financial.read", "assets.create", "assets.edit", "assets.assign", "assets.capitalize", "assets.book.manage", "assets.depreciation.prepare", "assets.depreciation.post", "assets.depreciation.reverse", "assets.disposal.manage", "assets.impair", "assets.revalue", "assets.verification.manage", "assets.opening.manage", "assets.audit.read", "assets.documents.read", "assets.documents.manage", "assets.export"],
        new(["fixed-assets.registry", "fixed-assets.custody", "fixed-assets.books", "fixed-assets.depreciation", "fixed-assets.capitalization", "fixed-assets.reconciliation"], ["platform.organization", "platform.numbering", "finbase.financial-context", "ledger.semantic-facts", "catalog.product", "inventory.history", "purchasing.invoice-source", "expenses.claims", "hr.core", "hr.fixed-assets", "platform.files", "platform.approvals", "platform.outbox"]),
        new(["FixedAssetCreatedV1", "FixedAssetAssignedV1", "FixedAssetCapitalizedV1", "FixedAssetDepreciatedV1", "FixedAssetDisposedV1"], ["StockIssuedV1", "InvoicePostedV1", "ExpenseFinalizedV1", "EmployeeOffboardingStartedV1"]), new(true, "blocked_if_dependents"));
}
