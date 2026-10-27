using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Modules.Payments.Repositories;
using Modules.Payments.Services;
using Shared.Primitives.Identifiers;

namespace Modules.Payments;

public static class PaymentsModuleExtensions
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, PaymentsModuleManifestProvider>();
        services.AddSingleton<PaymentsRepository>();
        services.AddSingleton<PaymentsService>();
        services.AddSingleton<IReferencePaymentProvider, ReferencePaymentProvider>();
        services.AddSingleton<IProviderSecretResolver, EnvironmentProviderSecretResolver>();
        return services;
    }

    public static WebApplication MapPaymentsModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Payments").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/legal-entities/{entityId:guid}/accounts", async (Guid entityId, Guid? branchId, HttpContext http, PaymentsService service,
            IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.account.read", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return Results.Ok(await service.ListAccountsAsync(access.Tenant, entityId, branchId, ct));
        });
        group.MapPost("/legal-entities/{entityId:guid}/accounts", async (Guid entityId, CreatePaymentAccountRequest request, HttpContext http,
            PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (entityId != request.LegalEntityId) return Results.BadRequest(new { code = "payments.entity_scope_invalid" });
            var permission = request.Type == PaymentAccountType.Cashbox ? "payments.cashbox.manage" : "payments.account.manage";
            var access = await AuthorizeAsync(http, subjects, authorizer, permission, entityId, request.BranchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try
            {
                var account = await service.CreateAccountAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct);
                return Results.Created($"/api/v1/payments/legal-entities/{entityId:D}/accounts/{account.Id:D}", account);
            }
            catch (PaymentException exception) { return Render(exception); }
        });
        group.MapGet("/legal-entities/{entityId:guid}", async (Guid entityId, Guid? branchId, int? page, int? pageSize, string? search,
            PaymentDirection? direction, PaymentBusinessState? state, DateOnly? fromDate, DateOnly? toDate, HttpContext http,
            PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return Results.Ok(await service.ListAsync(access.Tenant, new(entityId, page ?? 1, pageSize ?? 50, search, direction, state, fromDate, toDate, branchId), ct));
        });
        group.MapGet("/legal-entities/{entityId:guid}/{paymentId:guid}", async (Guid entityId, Guid paymentId, Guid? branchId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var payment = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct); return payment is null ? Results.NotFound() : Results.Ok(payment);
        });
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/complete", async (Guid entityId, Guid paymentId, Guid? branchId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.complete_manual", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var payment = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct);
            if (payment is null) return Results.NotFound();
            var paymentAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.complete_manual", entityId, payment.BranchId, ct);
            if (paymentAccess is null) return Results.Unauthorized(); if (!paymentAccess.Allowed) return Results.Forbid();
            if (payment.Method == PaymentMethod.Cash)
            {
                var cashboxAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.cashbox.use", entityId, payment.BranchId, ct);
                if (cashboxAccess is null) return Results.Unauthorized(); if (!cashboxAccess.Allowed) return Results.Forbid();
            }
            try { return Results.Ok(await service.CompleteManualAsync(access.Tenant, access.ActorId, access.MembershipId, entityId, paymentId, key, Correlation(http), ct)); }
            catch (PaymentException exception) { return Render(exception); }
        });
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/cancel", async (Guid entityId, Guid paymentId, Guid? branchId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.cancel_pending", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.CancelPendingAsync(access.Tenant, access.ActorId, access.MembershipId, entityId, branchId, paymentId, key, Correlation(http), ct)); }
            catch (PaymentException exception) { return Render(exception); }
        });
        group.MapPut("/legal-entities/{entityId:guid}/accounts/{accountId:guid}/status", async (Guid entityId, Guid accountId, UpdatePaymentAccountStatusRequest request,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.account.manage", entityId, request.BranchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try
            {
                await service.UpdateAccountStatusAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, accountId, request.BranchId, request.Status, ct);
                return Results.NoContent();
            }
            catch (PaymentException exception) { return Render(exception); }
        });
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/refunds", async (Guid entityId, Guid paymentId, CreateRefundRequest request,
            Guid? branchId, HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.refund.create", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var original = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct);
            if (original is null) return Results.NotFound();
            var originalAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.refund.create", entityId, original.BranchId, ct);
            if (originalAccess is null) return Results.Unauthorized(); if (!originalAccess.Allowed) return Results.Forbid();
            if (original.Direction != PaymentDirection.Incoming) return Results.Conflict(new { code = "payments.refund_original_unavailable" });
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            if (request.Method == PaymentMethod.Cash)
            {
                var cashboxAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.cashbox.use", entityId, original.BranchId, ct);
                if (cashboxAccess is null) return Results.Unauthorized(); if (!cashboxAccess.Allowed) return Results.Forbid();
            }
            try
            {
                var refund = await service.CreateRefundDraftAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, paymentId, request, key, ct);
                return Results.Created($"/api/v1/payments/legal-entities/{entityId:D}/{refund.Id:D}", refund);
            }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentRecord>(StatusCodes.Status201Created);
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/provider-refunds", async (Guid entityId, Guid paymentId, CreateRefundRequest request,
            Guid? branchId, HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.refund.create", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var original = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct);
            if (original is null) return Results.NotFound();
            var originalAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.refund.create", entityId, original.BranchId, ct);
            if (originalAccess is null) return Results.Unauthorized(); if (!originalAccess.Allowed) return Results.Forbid();
            if (original.Direction != PaymentDirection.Incoming) return Results.Conflict(new { code = "payments.refund_original_unavailable" });
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            try
            {
                var refund = await service.CreateProviderRefundAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, paymentId, request, key, ct);
                return Results.Accepted($"/api/v1/payments/legal-entities/{entityId:D}/{refund.Refund.Id:D}", refund);
            }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<ProviderRefundResult>(StatusCodes.Status202Accepted);
        group.MapPost("/legal-entities/{entityId:guid}/{refundPaymentId:guid}/provider-refund-status", async (Guid entityId, Guid refundPaymentId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.reconcile", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.SyncProviderRefundStatusAsync(access.Tenant, access.ActorId, access.MembershipId, entityId, refundPaymentId, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<ProviderRefundResult>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/reverse", async (Guid entityId, Guid paymentId, ReversePaymentRequest request,
            Guid? branchId, HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.reverse", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var original = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct);
            if (original is null) return Results.NotFound();
            var scoped = await AuthorizeAsync(http, subjects, authorizer, "payments.reverse", entityId, original.BranchId, ct);
            if (scoped is null) return Results.Unauthorized(); if (!scoped.Allowed) return Results.Forbid();
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            try { return Results.Ok(await service.ReverseAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, entityId, paymentId, key, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentRecord>(StatusCodes.Status200OK);
        group.MapGet("/legal-entities/{entityId:guid}/{paymentId:guid}/fees", async (Guid entityId, Guid paymentId, Guid? branchId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var payment = await service.GetAsync(access.Tenant, entityId, branchId, paymentId, ct);
            if (payment is null) return Results.NotFound();
            var scoped = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, payment.BranchId, ct);
            if (scoped is null) return Results.Unauthorized(); if (!scoped.Allowed) return Results.Forbid();
            return Results.Ok(await service.ListFeesAsync(access.Tenant, entityId, paymentId, ct));
        }).Produces<IReadOnlyList<PaymentFeeResult>>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/fees", async (Guid entityId, Guid paymentId, CreatePaymentFeeRequest request,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.fee.override", entityId, null, ct);
            if (access is null) return Results.Unauthorized();
            var payment = await service.GetAsync(access.Tenant, entityId, null, paymentId, ct);
            if (payment is null) return Results.NotFound();
            var scoped = await AuthorizeAsync(http, subjects, authorizer, "payments.fee.override", entityId, payment.BranchId, ct);
            if (scoped is null) return Results.Unauthorized(); if (!access.Allowed || !scoped.Allowed) return Results.Forbid();
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            try { return Results.Created($"/api/v1/payments/legal-entities/{entityId:D}/{paymentId:D}/fees", await service.AddFeeAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, paymentId, request, key, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentFeeResult>(StatusCodes.Status201Created);
        group.MapPost("/legal-entities/{entityId:guid}/{paymentId:guid}/applications", async (Guid entityId, Guid paymentId, ApplyPaymentRequest request,
            Guid? branchId, HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "subledger.application.create", entityId, branchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try
            {
                var result = await service.ApplyToPaymentAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, branchId, paymentId, request, key, ct);
                return Results.Ok(result);
            }
            catch (PaymentException exception) { return Render(exception); }
        });
        group.MapPost("/legal-entities/{entityId:guid}/internal-transfers", async (Guid entityId, CreateInternalTransferRequest request,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (entityId != request.LegalEntityId) return Results.BadRequest(new { code = "payments.entity_scope_invalid" });
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.internal_transfer.create", entityId, null, ct);
            if (access is null) return Results.Unauthorized();
            var accounts = await service.ListAccountsAsync(access.Tenant, entityId, null, ct);
            var source = accounts.SingleOrDefault(account => account.Id == request.SourcePaymentAccountId);
            var destination = accounts.SingleOrDefault(account => account.Id == request.DestinationPaymentAccountId);
            if (source is null || destination is null)
            {
                if (!access.Allowed) return Results.Forbid();
                return Results.NotFound(new { code = "payments.account_not_found" });
            }
            var sourceScope = await AuthorizeAsync(http, subjects, authorizer, "payments.internal_transfer.create", entityId, source.BranchId, ct);
            var destinationScope = await AuthorizeAsync(http, subjects, authorizer, "payments.internal_transfer.create", entityId, destination.BranchId, ct);
            if (sourceScope is null || destinationScope is null) return Results.Unauthorized();
            if (!access.Allowed && (!sourceScope.Allowed || !destinationScope.Allowed)) return Results.Forbid();
            foreach (var account in new[] { source, destination }.Where(account => account.Type == PaymentAccountType.Cashbox))
            {
                var cashbox = await AuthorizeAsync(http, subjects, authorizer, "payments.cashbox.use", entityId, account.BranchId, ct);
                if (cashbox is null) return Results.Unauthorized(); if (!cashbox.Allowed) return Results.Forbid();
            }
            try { return Results.Ok(await service.CreateInternalTransferAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, key, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<InternalTransferResult>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/payment-requests", async (Guid entityId, CreatePaymentIntentRequest request, HttpContext http,
            PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (entityId != request.LegalEntityId) return Results.BadRequest(new { code = "payments.entity_scope_invalid" });
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.incoming.create", entityId, request.BranchId, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Accepted(value: await service.CreatePaymentIntentAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, key, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentIntentResult>(StatusCodes.Status202Accepted);
        group.MapGet("/legal-entities/{entityId:guid}/payment-requests", async (Guid entityId, int? page, int? pageSize, string? search, string? state,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.ListPaymentRequestsAsync(access.Tenant, new(entityId, page ?? 1, pageSize ?? 50, search, state), ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentRequestPage>(StatusCodes.Status200OK);
        group.MapGet("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}", async (Guid entityId, Guid requestId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.read", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            var details = await service.GetPaymentRequestDetailsAsync(access.Tenant, entityId, requestId, ct);
            return details is null ? Results.NotFound(new { code = "payments.request_not_found" }) : Results.Ok(details);
        }).Produces<PaymentRequestDetails>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}/attempts", async (Guid entityId, Guid requestId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.retry", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Accepted(value: await service.RetryPaymentIntentAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), entityId, requestId, key, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentIntentResult>(StatusCodes.Status202Accepted);
        group.MapPost("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}/status-sync", async (Guid entityId, Guid requestId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.reconcile", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.SyncReferenceProviderStatusAsync(access.Tenant, entityId, requestId, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentIntentResult>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}/cancel", async (Guid entityId, Guid requestId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.cancel_pending", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.CancelProviderPaymentRequestAsync(access.Tenant, entityId, requestId, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentIntentResult>(StatusCodes.Status200OK);
        group.MapPost("/legal-entities/{entityId:guid}/providers", async (Guid entityId, CreatePaymentProviderRequest request, HttpContext http,
            PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (entityId != request.LegalEntityId) return Results.BadRequest(new { code = "payments.entity_scope_invalid" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.manage", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Created($"/api/v1/payments/legal-entities/{entityId:D}/providers", await service.CreateProviderConfigAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentProviderResult>(StatusCodes.Status201Created);
        group.MapPost("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}/links", async (Guid entityId, Guid requestId, CreatePaymentLinkRequest request,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            if (request.PaymentRequestId != requestId) return Results.BadRequest(new { code = "payments.request_scope_invalid" });
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.manage", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { return Results.Ok(await service.CreatePaymentLinkAsync(access.Tenant, request, entityId, ct)); }
            catch (PaymentException exception) { return Render(exception); }
        }).Produces<PaymentLinkResult>(StatusCodes.Status200OK);
        group.MapDelete("/legal-entities/{entityId:guid}/payment-requests/{requestId:guid}/links/{linkId:guid}", async (Guid entityId, Guid requestId, Guid linkId,
            HttpContext http, PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AuthorizeAsync(http, subjects, authorizer, "payments.provider.manage", entityId, null, ct);
            if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            try { await service.CancelPaymentLinkAsync(access.Tenant, entityId, requestId, linkId, ct); return Results.NoContent(); }
            catch (PaymentException exception) { return Render(exception); }
        });
        app.MapPost("/api/v1/payments/webhooks/reference/{tenantId:guid}/{providerConfigId:guid}", async (Guid tenantId, Guid providerConfigId,
            HttpContext http, PaymentsService service, CancellationToken ct) =>
        {
            if (http.Request.ContentLength is > 65536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            await using var body = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = await http.Request.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (body.Length + read > 65536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await body.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (body.Length == 0) return Results.BadRequest(new { code = "payments.webhook_payload_invalid" });
            try
            {
                await service.ReceiveReferenceWebhookAsync(new TenantId(tenantId), providerConfigId, body.ToArray(),
                    http.Request.Headers["X-Provider-Signature"].FirstOrDefault() ?? string.Empty, ct);
                return Results.NoContent();
            }
            catch (PaymentException exception) when (exception.Code == "payments.webhook_signature_invalid")
            { return Results.Unauthorized(); }
            catch (PaymentException exception) when (exception.Code == "payments.webhook_secret_unavailable")
            { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
            catch (PaymentException exception) { return Render(exception); }
        }).AllowAnonymous().WithTags("Payments").RequireRateLimiting("api");
        app.MapGet("/api/v1/payments/public-links/{token}", async (string token, PaymentsService service, CancellationToken ct) =>
        {
            var link = await service.GetPublicPaymentLinkAsync(token, ct);
            return link is null ? Results.NotFound() : Results.Ok(link);
        }).Produces<PublicPaymentLinkDetails>(StatusCodes.Status200OK).AllowAnonymous().WithTags("Payments");
        MapDirection("incoming", "payments.incoming.create", true); MapDirection("outgoing", "payments.outgoing.create", false);
        return app;

        void MapDirection(string route, string permission, bool incoming)
        {
            group.MapPost($"/legal-entities/{{entityId:guid}}/{route}", async (Guid entityId, CreatePaymentRequest request, HttpContext http,
                PaymentsService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
            {
                if (entityId != request.LegalEntityId) return Results.BadRequest(new { code = "payments.entity_scope_invalid" });
                if (request.Type is PaymentType.TransferLeg or PaymentType.ProviderFee or PaymentType.CustomerRefund) return Results.BadRequest(new { code = "payments.payment_type_invalid" });
                if (incoming != (request.Type is PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierRefund))
                    return Results.BadRequest(new { code = "payments.payment_type_direction_invalid" });
                var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(idempotencyKey)) return Results.BadRequest(new { code = "payments.idempotency_key_required" });
                var access = await AuthorizeAsync(http, subjects, authorizer, permission, entityId, request.BranchId, ct);
                if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
                if (request.Method == PaymentMethod.Cash)
                {
                    var cashboxAccess = await AuthorizeAsync(http, subjects, authorizer, "payments.cashbox.use", entityId, request.BranchId, ct);
                    if (cashboxAccess is null) return Results.Unauthorized(); if (!cashboxAccess.Allowed) return Results.Forbid();
                }
                try
                {
                    var payment = await service.CreateDraftAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, idempotencyKey, ct);
                    return Results.Created($"/api/v1/payments/legal-entities/{entityId:D}/{payment.Id:D}", payment);
                }
                catch (PaymentException exception) { return Render(exception); }
            }).Produces<PaymentRecord>(StatusCodes.Status201Created);
        }
    }

    private static async Task<Access?> AuthorizeAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer,
        string permission, Guid entityId, Guid? branchId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantValue)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var tenant = new TenantId(tenantValue); var subject = await subjects.LoadAsync(tenant, membershipId, ct); if (subject is null) return null;
        var scope = branchId is { } branch ? PermissionScope.ForBranch(tenant, new(entityId), new(branch)) : PermissionScope.ForLegalEntity(tenant, new(entityId));
        return new(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, scope));
    }

    private static IResult Render(PaymentException exception)
    {
        var status = exception.Code switch
        {
            "payments.payment_not_found" or "payments.account_not_found" or "payments.refund_original_unavailable" => 404,
            "payments.idempotency_conflict" or "payments.account_inactive" or "payments.account_archived" or "payments.refund_exceeds_refundable_amount" or "payments.refund_state_invalid"
                or "payments.reversal_not_available" or "payments.reversal_has_applications" or "payments.reversal_already_exists" or "payments.reversal_refund_exists"
                or "payments.link_request_unavailable" or "payments.link_not_cancellable" => 409,
            "payments.request_not_found" => 404,
            _ => 422
        };
        return Results.Problem(title: exception.Message, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
    }

    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-ID"].FirstOrDefault(), out var id) ? id : Guid.CreateVersion7();
    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed);
}

public sealed class PaymentsModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("payments", "1.0.0", 1, "L3", "module", false, "tenant",
        new(["platform-kernel", "directory", "finbase", "ledger", "subledger"], ["platform.files"]),
        new("finance_payments", ["payment", "payment_account", "payment_method_config", "payment_request", "payment_attempt", "payment_fee", "payment_fee_disposition", "payment_refund", "payment_reversal", "payment_relationship", "internal_transfer", "provider_config", "provider_operation", "provider_webhook_inbox", "payment_attachment_ref", "payment_list_projection", "payment_idempotency", "payment_link"]),
        ["payments.read", "payments.incoming.create", "payments.outgoing.create", "payments.internal_transfer.create", "payments.complete_manual", "payments.cancel_pending", "payments.reverse", "payments.refund.create", "payments.refund.approve", "payments.account.read", "payments.account.manage", "payments.provider.read", "payments.provider.manage", "payments.provider.retry", "payments.provider.reconcile", "payments.cashbox.read", "payments.cashbox.use", "payments.cashbox.manage", "payments.fee.override", "payments.fx.override", "payments.date.override", "payments.audit.view"],
        new(["payments.money-movement", "payments.payment-accounts", "payments.provider-adapter", "payments.provider-webhooks", "payments.refunds", "payments.internal-transfers", "payments.payment-links", "payments.payment-read-model"], ["directory.party", "finbase.money", "finbase.exchange-rate", "subledger.open-items", "ledger.semantic-facts", "platform.numbering", "platform.audit", "platform.transaction-coordination", "platform.outbox", "platform.files"]),
        new(["PaymentCompletedV1", "PaymentReversedV1", "PaymentRefundedV1", "InternalTransferCompletedV1", "PaymentProviderOperationRequestedV1"], []), new(false, "blocked_if_dependents"));
}
