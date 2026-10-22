using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tax.DTOs;
using Modules.Tax.Entities;
using Modules.Tax.Services;
using Shared.Contracts.Tax;
using Shared.Primitives.Identifiers;

namespace Modules.Tax;

public static class TaxModuleExtensions
{
    public static IServiceCollection AddTaxModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, TaxModuleManifestProvider>();
        services.AddSingleton<Repositories.TaxRepository>();
        services.AddSingleton<Repositories.ITaxRepository>(provider => provider.GetRequiredService<Repositories.TaxRepository>());
        services.AddSingleton<Repositories.TaxLifecycleRepository>();
        services.AddSingleton<Repositories.TaxRegistrationRepository>();
        services.AddSingleton<Repositories.TaxProfileRepository>();
        services.AddSingleton<Repositories.TaxProfileImportRepository>();
        services.AddSingleton<ITaxCountryPackRegistry, TaxCountryPackRegistry>();
        services.AddSingleton<TaxService>();
        services.AddSingleton<ITaxCapabilities>(provider => provider.GetRequiredService<TaxService>());
        return services;
    }

    public static WebApplication MapTaxModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tax").RequireAuthorization().RequireRateLimiting("api").WithTags("Tax");
        group.MapGet("/coverage", async (Guid legalEntityId, DateOnly at, string countryCode, string? schemeCode, Guid? productTemplateId, Guid? productVariantId, Guid? partyId, string? direction, string? documentKind, string? transactionType, string? jurisdictionCode, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
        {
            var access = await Authorized(http, subjects, authorizer, "tax.read", legalEntityId, ct);
            return access is { Allowed: true }
                ? await Execute(() => repository.GetCoverageAsync(access.Tenant.Value, new(legalEntityId, at, countryCode, schemeCode, productTemplateId, productVariantId, partyId, direction, documentKind, transactionType, jurisdictionCode), ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.read", legalEntityId, ct);
        }).WithName("GetTaxCoverage");
        group.MapPost("/imports/product-profiles/preview", async (TaxProfileImportPreviewRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileImportRepository repository, CancellationToken ct) =>
        {
            var access = await Authorized(http, subjects, authorizer, "tax.import", request.LegalEntityId, ct);
            if (access is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.import", request.LegalEntityId, ct);
            var manager = await Authorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId, ct);
            if (manager is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId, ct);
            return await Execute(() => repository.PreviewAsync(access.Tenant.Value, access.ActorId, Correlation(http), request with { Kind = "Product" }, ct));
        });
        group.MapPost("/imports/party-profiles/preview", async (TaxProfileImportPreviewRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileImportRepository repository, CancellationToken ct) =>
        {
            var access = await Authorized(http, subjects, authorizer, "tax.import", request.LegalEntityId, ct);
            if (access is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.import", request.LegalEntityId, ct);
            var manager = await Authorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId, ct);
            if (manager is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId, ct);
            return await Execute(() => repository.PreviewAsync(access.Tenant.Value, access.ActorId, Correlation(http), request with { Kind = "Party" }, ct));
        });
        group.MapPost("/imports/{batchId:guid}/commit", async (Guid batchId, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileImportRepository repository, CancellationToken ct) =>
        {
            var access = await Authorized(http, subjects, authorizer, "tax.import", legalEntityId, ct);
            if (access is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.import", legalEntityId, ct);
            (string Kind, Guid LegalEntityId) batch;
            try { batch = await repository.GetBatchTypeAsync(access.Tenant.Value, batchId, access.ActorId, ct); }
            catch (TaxException exception) { return TaxProblem(exception); }
            if (batch.LegalEntityId != legalEntityId) return Results.NotFound();
            var permission = batch.Kind == "PartyTaxProfile" ? "tax.party_profile.manage" : "tax.product_profile.manage";
            var manager = await Authorized(http, subjects, authorizer, permission, legalEntityId, ct);
            if (manager is null) return Results.Unauthorized();
            if (!manager.Allowed) return Results.Forbid();
            return await Execute(() => repository.CommitAsync(access.Tenant.Value, access.ActorId, Correlation(http), batchId, ct));
        });
        group.MapGet("/exports/{kind}", async (string kind, Guid legalEntityId, DateOnly? at, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileImportRepository repository, CancellationToken ct) =>
        {
            var access = await Authorized(http, subjects, authorizer, "tax.export", legalEntityId, ct);
            if (access is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.export", legalEntityId, ct);
            var readPermission = string.Equals(kind, "Party", StringComparison.OrdinalIgnoreCase) ? "tax.party_profile.read" : "tax.product_profile.read";
            var reader = await Authorized(http, subjects, authorizer, readPermission, legalEntityId, ct);
            if (reader is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, readPermission, legalEntityId, ct);
            if (string.Equals(kind, "Party", StringComparison.OrdinalIgnoreCase))
            {
                var registrationReader = await Authorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct);
                if (registrationReader is not { Allowed: true }) return await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct);
            }
            return await Execute(() => repository.ExportAsync(access.Tenant.Value, legalEntityId, kind, at, ct));
        });
        group.MapGet("/legal-entities/{entityId:guid}/capability", async (Guid entityId, DateOnly? at, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, TaxService service, CancellationToken ct) =>
            at is null ? Results.Problem(statusCode:400,title:"An effective date is required to read Tax Capability history.",extensions:new Dictionary<string,object?>{{"code","tax.effective_date_required"}}) :
            await Authorized(http, subjects, authorizer, "tax.read", entityId, ct) is { Allowed: true } access
                ? await Execute(() => service.GetCapabilityAsync(access.Tenant.Value, entityId, at.Value, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.read", entityId, ct));
        group.MapPut("/legal-entities/{entityId:guid}/capability", async (Guid entityId, SetTaxCapabilityRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, TaxService service, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.capability.configure", entityId, ct) is { Allowed: true } access
                ? await Execute(() => service.SetCapabilityAsync(access.Tenant.Value, entityId, access.ActorId, request, Correlation(http), ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.capability.configure", entityId, ct));
        group.MapPost("/calculate", async (TaxCalculationRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, TaxService service, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.simulate", request.LegalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => service.CalculateAsync(ToContract(access.Tenant, request), ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.simulate", request.LegalEntityId, ct));
        group.MapPost("/calculate-batch", async (TaxCalculationBatchRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, TaxService service, CancellationToken ct) =>
        {
            if (request.Items.Count is < 1 or > 500) return Results.Problem(statusCode:400,title:"Tax batches must contain between one and five hundred lines.",extensions:new Dictionary<string,object?>{{"code","tax.batch_size_invalid"}});
            var accesses = new List<TaxAccess>();
            foreach (var item in request.Items)
            {
                var access = await Authorized(http, subjects, authorizer, "tax.simulate", item.LegalEntityId, ct);
                if (access is null) return Results.Unauthorized();
                if (!access.Allowed) return Results.Forbid();
                accesses.Add(access);
            }
            if (accesses.Any(access => access.Tenant != accesses[0].Tenant)) return Results.Problem(statusCode:400,title:"A Tax batch must contain one tenant.",extensions:new Dictionary<string,object?>{{"code","tax.batch_tenant_mismatch"}});
            return await Execute(() => service.CalculateBatchAsync(request.Items.Select(item => ToContract(accesses[0].Tenant,item)).ToArray(), ct));
        });
        group.MapPost("/rule-sets", async (TaxRuleSetRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", request.LegalEntityId ?? Guid.Empty, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreateRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", request.LegalEntityId ?? Guid.Empty, ct));
        group.MapGet("/rule-sets", async (Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListRuleSetsAsync(access.Tenant.Value, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct));
        group.MapGet("/rule-sets/{id:guid}", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.GetRuleSetAsync(access.Tenant.Value, id, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct));
        group.MapGet("/reference-data", async (Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.policy.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ReadReferenceDataAsync(access.Tenant.Value, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.policy.read", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/rules", async (Guid id, Guid legalEntityId, TaxRuleRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.AddRuleAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct));
        group.MapGet("/rule-sets/{id:guid}/rules", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListRulesAsync(access.Tenant.Value, id, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.read", legalEntityId, ct));
        group.MapPut("/rules/{id:guid}", async (Guid id, Guid legalEntityId, TaxRuleRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.UpdateRuleAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/validate", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(async () => { await repository.ValidateRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, ct); return new { validated = true, id }; })
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/schedule", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.TransitionRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, "Scheduled", ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/approve", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.TransitionRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, "Approved", ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/activate", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.TransitionRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, "Active", ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/supersede", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.TransitionRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, "Superseded", ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.activate", legalEntityId, ct));
        group.MapPost("/rule-sets/{id:guid}/retire", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxLifecycleRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.TransitionRuleSetAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, "Retired", ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.rule.manage", legalEntityId, ct));
        group.MapPost("/registrations", async (TaxRegistrationRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.manage", request.LegalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreateAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.manage", request.LegalEntityId, ct));
        group.MapGet("/registrations", async (Guid legalEntityId, string? schemeCode, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListAsync(access.Tenant.Value, legalEntityId, schemeCode, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct));
        group.MapGet("/registrations/{id:guid}", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.GetAsync(access.Tenant.Value, id, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct));
        group.MapGet("/registrations/{id:guid}/history", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.HistoryAsync(access.Tenant.Value, id, legalEntityId, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct));
        group.MapPatch("/registrations/{id:guid}", async (Guid id, Guid legalEntityId, TaxRegistrationChangeRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ChangeAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, request, false, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct));
        group.MapPost("/registrations/{id:guid}/deregister", async (Guid id, Guid legalEntityId, TaxRegistrationChangeRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ChangeAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, request, true, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct));
        group.MapPost("/registrations/{id:guid}/members", async (Guid id, TaxRegistrationMemberRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.manage", request.LegalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.AddMemberAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.manage", request.LegalEntityId, ct));
        group.MapGet("/registrations/{id:guid}/members", async (Guid id, Guid legalEntityId, DateOnly? at, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListMembersAsync(access.Tenant.Value, id, at, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.read", legalEntityId, ct));
        group.MapPost("/registration-members/{id:guid}/end", async (Guid id, Guid legalEntityId, DateOnly effectiveTo, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxRegistrationRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.EndMemberAsync(access.Tenant.Value, access.ActorId, Correlation(http), id, legalEntityId, effectiveTo, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.registration.manage", legalEntityId, ct));
        group.MapPost("/party-profiles", async (TaxPartyProfileRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId ?? Guid.Empty, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreatePartyAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId ?? Guid.Empty, ct));
        group.MapGet("/party-profiles", async (Guid partyId, Guid legalEntityId, DateOnly? at, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.party_profile.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListPartyAsync(access.Tenant.Value, partyId, legalEntityId, at, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.read", legalEntityId, ct));
        group.MapGet("/party-profiles/{id:guid}/history", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.party_profile.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.PartyHistoryAsync(access.Tenant.Value, id, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.read", legalEntityId, ct));
        group.MapPut("/party-profiles/{id:guid}", async (Guid id, TaxPartyProfileRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId ?? Guid.Empty, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreatePartyAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct, id))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.manage", request.LegalEntityId ?? Guid.Empty, ct));
        group.MapPost("/party-profiles/{id:guid}/end", async (Guid id, Guid legalEntityId, DateOnly effectiveTo, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.party_profile.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.EndAsync(access.Tenant.Value, access.ActorId, Correlation(http), true, id, effectiveTo, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.party_profile.manage", legalEntityId, ct));
        group.MapPost("/product-profiles", async (TaxProductProfileRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId ?? Guid.Empty, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreateProductAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId ?? Guid.Empty, ct));
        group.MapGet("/product-profiles", async (Guid productTemplateId, Guid? productVariantId, Guid legalEntityId, DateOnly? at, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.product_profile.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ListProductAsync(access.Tenant.Value, productTemplateId, productVariantId, legalEntityId, at, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.read", legalEntityId, ct));
        group.MapGet("/product-profiles/{id:guid}/history", async (Guid id, Guid legalEntityId, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.product_profile.read", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.ProductHistoryAsync(access.Tenant.Value, id, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.read", legalEntityId, ct));
        group.MapPut("/product-profiles/{id:guid}", async (Guid id, TaxProductProfileRequest request, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId ?? Guid.Empty, ct) is { Allowed: true } access
                ? await Execute(() => repository.CreateProductAsync(access.Tenant.Value, access.ActorId, Correlation(http), request, ct, id))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.manage", request.LegalEntityId ?? Guid.Empty, ct));
        group.MapPost("/product-profiles/{id:guid}/end", async (Guid id, Guid legalEntityId, DateOnly effectiveTo, HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, Repositories.TaxProfileRepository repository, CancellationToken ct) =>
            await Authorized(http, subjects, authorizer, "tax.product_profile.manage", legalEntityId, ct) is { Allowed: true } access
                ? await Execute(() => repository.EndAsync(access.Tenant.Value, access.ActorId, Correlation(http), false, id, effectiveTo, ct))
                : await ForbiddenOrUnauthorized(http, subjects, authorizer, "tax.product_profile.manage", legalEntityId, ct));
        return app;
    }

    private static TaxContextContract ToContract(TenantId tenant, TaxCalculationRequest request) => new(tenant,new(request.LegalEntityId),request.Direction,request.DocumentKind,request.TransactionType,request.DocumentDate,request.TaxDate,request.SupplyDate,request.PostingDate,request.PartyId,request.PartyCountryCode,request.PlaceOfSupplyCountryCode,request.JurisdictionCode,request.ProductTemplateId,request.ProductVariantId,request.Quantity,request.UnitOfMeasureId,request.PriceMode,request.CommercialAmount,request.DiscountAmount,request.TaxableCharges,request.TransactionCurrency,request.BaseCurrency,request.RegistrationId,request.TaxSchemeCode,request.DocumentSemantics,request.Dimensions);
    private static Task<TaxAccess?> Authorized(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
        => TaxAuthorization.ResolveAsync(http.User, subjects, authorizer, permission, entityId, ct);
    private static async Task<IResult> ForbiddenOrUnauthorized(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid entityId, CancellationToken ct)
    {
        var access = await Authorized(http,subjects,authorizer,permission,entityId,ct); return access is null ? Results.Unauthorized() : Results.Forbid();
    }
    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { var result = await action(); return result is null ? Results.NotFound() : Results.Ok(result); }
        catch (TaxException exception) { return TaxProblem(exception); }
    }
    private static IResult TaxProblem(TaxException exception)
    { var code=exception.Code; var status=code.Contains("missing",StringComparison.Ordinal) || code.Contains("conflict",StringComparison.Ordinal) || code.Contains("not_found",StringComparison.Ordinal) ? 409 : 400; return Results.Problem(statusCode:status,title:exception.Message,extensions:new Dictionary<string,object?>{{"code",code}}); }
    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"],out var value) ? value : Guid.CreateVersion7();
}

public sealed class TaxModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("tax","1.0.0",1,"L1","foundation",false,"tenant",new(["platform-kernel"],["platform.organization","directory.party","catalog.product","catalog.quantity-conversion","finbase.money","pricing.resolve"]),new("foundation_tax",["tax_capability_state","tax_registration","tax_registration_member","tax_scheme","tax_type","tax_jurisdiction","tax_category","tax_policy","party_tax_profile","product_tax_profile","tax_rule_set","tax_rule","tax_rule_version","tax_stack_definition","tax_component_definition","tax_pack_activation","tax_audit","tax_import_batch","tax_import_row","tax_snapshot"]),["tax.read","tax.registration.read","tax.registration.manage","tax.party_profile.read","tax.party_profile.manage","tax.product_profile.read","tax.product_profile.manage","tax.policy.read","tax.policy.manage","tax.rule.read","tax.rule.manage","tax.rule.activate","tax.simulate","tax.audit.view","tax.import","tax.export","tax.capability.configure"],new(["tax.resolve","tax.resolve-batch","tax.snapshot"],["platform.organization","directory.party","catalog.product","finbase.money","pricing.resolve"]),new(["TaxCapabilityChangedV1","TaxRegistrationChangedV1","TaxCountryPackActivatedV1","TaxRuleSetActivatedV1","ProductTaxProfileChangedV1","PartyTaxProfileChangedV1"],[]),new(true,"blocked_if_dependents"));
}
