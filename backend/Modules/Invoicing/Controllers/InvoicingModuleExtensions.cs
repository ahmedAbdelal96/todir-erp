using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Invoicing.DTOs;
using Modules.Invoicing.Entities;
using Modules.Invoicing.Repositories;
using Modules.Invoicing.Services;
using NodaTime;
using Shared.Primitives.Identifiers;
using Shared.Primitives.Time;

namespace Modules.Invoicing.Controllers;

public static class InvoicingModuleExtensions
{
    public static IServiceCollection AddInvoicingModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, InvoicingModuleManifestProvider>();
        services.AddSingleton<InvoicingRepository>();
        services.AddSingleton<InvoicingService>();
        services.AddSingleton<Shared.Contracts.Invoicing.IInvoicingSourceCapabilities>(provider => provider.GetRequiredService<InvoicingService>());
        services.AddSingleton<Shared.Contracts.Invoicing.IInvoicingCorrectionCapabilities>(provider => provider.GetRequiredService<InvoicingService>());
        services.AddSingleton<Shared.Contracts.Invoicing.IInvoiceComplianceSnapshotCapability>(provider => provider.GetRequiredService<InvoicingService>());
        services.AddSingleton<Shared.Contracts.Invoicing.IInvoiceFileReferenceCapability>(provider => provider.GetRequiredService<InvoicingService>());
        return services;
    }

    public static WebApplication MapInvoicingModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/invoicing").WithTags("Invoicing").RequireAuthorization().RequireRateLimiting("api");
        MapDirection(group, "customer-invoices", InvoiceDirection.Receivable, "invoicing.customer_invoice");
        MapDirection(group, "supplier-invoices", InvoiceDirection.Payable, "invoicing.supplier_invoice");
        return app;

        void MapDirection(RouteGroupBuilder routes, string path, InvoiceDirection direction, string permissionPrefix)
        {
            routes.MapGet($"/{path}", async (Guid legalEntityId, Guid? branchId, Guid? partyId, DateOnly? fromDate, DateOnly? toDate,
                string? currencyCode, string? lifecycle, string? search, int? page, int? pageSize, HttpContext http,
                InvoicingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".read", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                return await Execute(() => service.ListAsync(access.Tenant, new(legalEntityId, branchId, partyId, fromDate, toDate, currencyCode, lifecycle, search, page ?? 1, pageSize ?? 50), direction, ct), Results.Ok);
            }).Produces<InvoicePage>();

            routes.MapPost($"/{path}", async (CreateInvoiceDraftRequest request, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, ILegalEntityFinancialReadStore legalEntities, TimeProvider clock, CancellationToken ct) =>
            {
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".create", request.LegalEntityId, request.BranchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                if (!await AuthorizeOverridesAsync(http, subjects, authorizer, access.Tenant, legalEntities, request.LegalEntityId, request.BranchId, request.Lines?.Any(line => line?.UnitPrice is not null) == true,
                    await service.IsPaymentTermOverrideAsync(access.Tenant, request.LegalEntityId, request.PartyId, request.PaymentTermId, direction, ct),
                    request.DocumentDate, request.PostingDate, request.TaxDate, clock, ct)) return Results.Forbid();
                return await Execute(() => service.CreateDraftAsync(access.Tenant, access.ActorId, access.MembershipId, request, direction, Correlation(http), ct),
                    id => Results.Created($"/api/v1/invoicing/{path}/{id}", new { id, lifecycle = "Draft", version = 1 }));
            }).Produces(StatusCodes.Status201Created);

            MapCorrection("credit-notes", "CreditNote", "invoicing.credit_note.create");
            MapCorrection("debit-notes", "DebitNote", "invoicing.debit_note.create");

            void MapCorrection(string correctionPath, string documentType, string correctionPermission)
            {
                routes.MapPost($"/{path}/{{id:guid}}/{correctionPath}", async (Guid id, Guid legalEntityId, InvoiceCorrectionRequest request, HttpContext http,
                    InvoicingService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, ILegalEntityFinancialReadStore legalEntities, TimeProvider clock, CancellationToken ct) =>
                {
                    if (request.DocumentType != documentType)
                        return Results.BadRequest(new { code = "invoicing.correction_type_mismatch", message = "The correction type must match the selected action." });
                    if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                    Guid? branchId;
                    try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                    catch (InvoiceException) { return Results.NotFound(); }
                    var access = await AuthorizeAsync(http, subjects, authorizer, correctionPermission, legalEntityId, branchId, ct);
                    if (access is null) return Results.Unauthorized();
                    if (!access.Allowed) return Results.Forbid();
                    var documentAccess = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".create", legalEntityId, branchId, ct);
                    if (documentAccess is null) return Results.Unauthorized();
                    if (!documentAccess.Allowed) return Results.Forbid();
                    if (!await AuthorizeOverridesAsync(http, subjects, authorizer, access.Tenant, legalEntities, legalEntityId, branchId, false, false,
                            request.DocumentDate, request.PostingDate, request.TaxDate, clock, ct)) return Results.Forbid();
                    return await Execute(() => service.CreateCorrectionDraftAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId,
                            request, direction, Correlation(http), ct),
                        correctionId => Results.Created($"/api/v1/invoicing/{path}/{correctionId}", new { id = correctionId, lifecycle = "Draft", documentType, version = 1 }));
                }).Produces(StatusCodes.Status201Created);
            }

            routes.MapPost($"/{path}/preview", async (CreateInvoiceDraftRequest request, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, ILegalEntityFinancialReadStore legalEntities, TimeProvider clock, CancellationToken ct) =>
            {
                var access = await AuthorizeAsync(http, subjects, authorizer, "invoicing.accounting_preview", request.LegalEntityId, request.BranchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                if (!await AuthorizeOverridesAsync(http, subjects, authorizer, access.Tenant, legalEntities, request.LegalEntityId, request.BranchId, request.Lines?.Any(line => line?.UnitPrice is not null) == true,
                    await service.IsPaymentTermOverrideAsync(access.Tenant, request.LegalEntityId, request.PartyId, request.PaymentTermId, direction, ct),
                    request.DocumentDate, request.PostingDate, request.TaxDate, clock, ct)) return Results.Forbid();
                return await Execute(() => service.PreviewDraftAsync(access.Tenant, request.LegalEntityId, request, direction, ct), Results.Ok);
            }).Produces<InvoicePreview>();

            routes.MapGet($"/{path}/{{id:guid}}", async (Guid id, Guid legalEntityId, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".read", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                return await Execute(() => service.GetDraftResponseAsync(access.Tenant, legalEntityId, id, ct), Results.Ok);
            }).Produces<InvoiceDraftResponse>();

            routes.MapGet($"/{path}/{{id:guid}}/detail", async (Guid id, Guid legalEntityId, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".read", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                return await Execute(() => service.GetDetailAsync(access.Tenant, legalEntityId, id, ct), Results.Ok);
            }).Produces<InvoiceDetail>();

            routes.MapGet($"/{path}/{{id:guid}}/render-model", async (Guid id, Guid legalEntityId, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".read", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                return await Execute(() => service.GetRenderModelAsync(access.Tenant, legalEntityId, id, ct), Results.Ok);
            }).Produces<InvoiceRenderModel>();

            routes.MapGet($"/{path}/{{id:guid}}/compliance-snapshot", async (Guid id, Guid legalEntityId, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, "invoicing.regulatory_details", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                var snapshot = await service.GetInvoiceComplianceSnapshotAsync(access.Tenant, legalEntityId, id, ct);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }).Produces<Shared.Contracts.Invoicing.InvoiceComplianceSnapshotContract>();

            routes.MapPut($"/{path}/{{id:guid}}", async (Guid id, Guid legalEntityId, UpdateInvoiceDraftRequest request, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, ILegalEntityFinancialReadStore legalEntities, TimeProvider clock, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".edit", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                if (!await AuthorizeOverridesAsync(http, subjects, authorizer, access.Tenant, legalEntities, legalEntityId, branchId, request.Lines?.Any(line => line?.UnitPrice is not null) == true,
                    await service.IsPaymentTermOverrideAsync(access.Tenant, legalEntityId, request.PartyId, request.PaymentTermId, direction, ct),
                    request.DocumentDate, request.PostingDate, request.TaxDate, clock, ct)) return Results.Forbid();
                return await Execute(() => service.UpdateDraftAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId, request, Correlation(http), ct),
                    updated => Results.Ok(new { id, version = updated }));
            });

            routes.MapPost($"/{path}/{{id:guid}}/cancel-draft", async (Guid id, Guid legalEntityId, CancelInvoiceDraftRequest request, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".cancel_draft", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                return await Execute(async () =>
                {
                    await service.CancelDraftAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId, request, Correlation(http), ct);
                    return true;
                }, _ => Results.NoContent());
            });

            routes.MapPost($"/{path}/{{id:guid}}/preview", async (Guid id, Guid legalEntityId, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, "invoicing.accounting_preview", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                var draft = await service.GetDraftResponseAsync(access.Tenant, legalEntityId, id, ct);
                if (draft.Lines.Any(line => line.UnitPrice is not null)
                    && !await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.price_override", legalEntityId, branchId, ct)) return Results.Forbid();
                return await Execute(() => service.PreviewAsync(access.Tenant, legalEntityId, id, ct), Results.Ok);
            }).Produces<InvoicePreview>();

            routes.MapPost($"/{path}/{{id:guid}}/finalize", async (Guid id, Guid legalEntityId, InvoiceFinalizationRequest request, HttpContext http, InvoicingService service,
                IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)) return Results.Unauthorized();
                Guid? branchId;
                try { branchId = await service.GetBranchIdAsync(new(tenantValue), legalEntityId, id, ct); }
                catch (InvoiceException) { return Results.NotFound(); }
                var access = await AuthorizeAsync(http, subjects, authorizer, permissionPrefix + ".finalize", legalEntityId, branchId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                var draft = await service.GetDraftResponseAsync(access.Tenant, legalEntityId, id, ct);
                if (draft.Lines.Any(line => line.UnitPrice is not null)
                    && !await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.price_override", legalEntityId, branchId, ct)) return Results.Forbid();
                return await Execute(() => service.FinalizeAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId, request, Correlation(http), ct), Results.Ok);
            }).Produces<InvoiceFinalizedResult>();
        }
    }

    private static async Task<Access?> AuthorizeAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, Guid? branchId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"), out var actorId)) return null;
        var tenant = new TenantId(tenantValue);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return null;
        var scope = branchId is { } branch ? PermissionScope.ForBranch(tenant, new(entityId), new(branch)) : PermissionScope.ForLegalEntity(tenant, new(entityId));
        return new(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, scope));
    }

    private static async Task<bool> AuthorizeOverridesAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        TenantId tenant, ILegalEntityFinancialReadStore legalEntities, Guid entityId, Guid? branchId, bool hasPriceOverride, bool hasPaymentTermOverride,
        DateOnly documentDate, DateOnly postingDate, DateOnly taxDate, TimeProvider clock, CancellationToken ct)
    {
        if (hasPriceOverride && !await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.price_override", entityId, branchId, ct)) return false;
        if (hasPaymentTermOverride && !await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.payment_term_override", entityId, branchId, ct)) return false;
        var timeZoneId = await legalEntities.GetTimeZoneAsync(tenant, new LegalEntityId(entityId), ct);
        var timeZone = timeZoneId is null ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId);
        if (timeZone is null)
            return await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.date_override", entityId, branchId, ct);
        var today = BusinessTime.ToLocalDate(Instant.FromDateTimeOffset(clock.GetUtcNow()), timeZone).ToDateOnly();
        return (documentDate == today && postingDate == today && taxDate == today)
            || await HasInvoicePermissionAsync(http, subjects, authorizer, "invoicing.date_override", entityId, branchId, ct);
    }

    private static async Task<bool> HasInvoicePermissionAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        string permission, Guid entityId, Guid? branchId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)) return false;
        var tenant = new TenantId(tenantValue);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return false;
        var scope = branchId is { } branch ? PermissionScope.ForBranch(tenant, new(entityId), new(branch)) : PermissionScope.ForLegalEntity(tenant, new(entityId));
        return authorizer.Authorize(subject, permission, scope);
    }

    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-ID"], out var value) ? value : Guid.CreateVersion7();

    private static async Task<IResult> Execute<T>(Func<Task<T>> action, Func<T, IResult> success)
    {
        try { return success(await action()); }
        catch (InvoiceException exception)
        {
            var status = exception.Code switch
            {
                "invoicing.invoice_not_found" => 404,
                "invoicing.already_finalized" or "invoicing.draft_changed" or "invoicing.finalization_conflict" or "invoicing.idempotency_conflict" or "invoicing.source_idempotency_conflict" or "invoicing.supplier_duplicate" => 409,
                "invoicing.finalization_blocked" or "invoicing.price_missing" or "invoicing.product_not_ready" or "invoicing.price_currency_mismatch" or "invoicing.tax_not_ready" or "invoicing.financial_context_unavailable" or "invoicing.exchange_rate_missing" => 422,
                _ => 400
            };
            return Results.Problem(title: exception.Message, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (Shared.Contracts.FinanceBase.FinanceBaseCapabilityException exception)
        {
            var code = exception.GetType().GetProperty("Code")?.GetValue(exception)?.ToString() ?? "invoicing.dependency_not_ready";
            return Results.Problem(title: "The invoice could not be processed with the current business configuration.", statusCode: 422, extensions: new Dictionary<string, object?> { ["code"] = code });
        }
        catch (Exception exception) when (TryGetCode(exception) is { } code)
        {
            return Results.Problem(title: "The invoice could not be processed with the current business configuration.", statusCode: 422, extensions: new Dictionary<string, object?> { ["code"] = code });
        }
    }

    private static string? TryGetCode(Exception exception) => exception.GetType().GetProperty("Code")?.GetValue(exception) as string;

    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class InvoicingModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new(
        "invoicing", "1.0.0", 1, "L3", "module", false, "tenant",
        new(["platform-kernel", "directory", "catalog", "finbase", "pricing", "tax", "ledger", "subledger"], ["platform.files"]),
        new("business_invoicing", ["invoice", "invoice_line", "invoice_party_snapshot", "invoice_address_snapshot", "invoice_price_snapshot", "invoice_tax_component", "invoice_payment_schedule", "invoice_charge", "invoice_charge_allocation", "invoice_discount_allocation", "invoice_relationship", "invoice_attachment", "invoice_issued_artifact", "invoice_finalization_record", "invoice_number_reference", "invoice_list_projection", "invoice_compliance_snapshot"]),
        ["invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize", "invoicing.customer_invoice.cancel_draft", "invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize", "invoicing.credit_note.create", "invoicing.debit_note.create", "invoicing.price_override", "invoicing.payment_term_override", "invoicing.date_override", "invoicing.accounting_preview", "invoicing.regulatory_details", "invoicing.attachment.manage", "invoicing.delivery.send", "invoicing.audit.view"],
        new(["invoicing.financial-documents", "invoicing.invoice-snapshots", "invoicing.invoice-corrections", "invoicing.source-draft", "invoicing.compliance-snapshot", "invoicing.file-references"], ["directory.party", "catalog.product-uom", "pricing.batch-resolution", "tax.batch-calculation", "tax.invoice-document-validation", "finbase.money", "finbase.payment-terms", "subledger.open-items", "ledger.semantic-facts", "platform.numbering", "platform.audit", "platform.transaction-coordination", "platform.outbox", "platform.files"]),
        new(["InvoiceDraftCreatedV1", "InvoiceDraftCancelledV1", "InvoiceFinalizedV1", "InvoiceCorrectedV1"], []),
        new(false, "blocked_if_dependents"));
}
