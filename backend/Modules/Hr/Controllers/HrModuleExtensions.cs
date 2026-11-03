using System.Security.Claims;
using Core.Authorization;
using Core.Modules;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Modules.Hr.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.Primitives.Identifiers;

namespace Modules.Hr;

public static class HrModuleExtensions
{
    public static IServiceCollection AddHrModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleManifestProvider, HrModuleManifestProvider>();
        services.AddSingleton<HrRepository>();
        services.AddSingleton<Shared.Contracts.Hr.IHumanResourcesLifecycleCapability>(provider => provider.GetRequiredService<HrRepository>());
        services.AddSingleton<HrImportRepository>();
        services.AddSingleton<HrAssignmentImportRepository>();
        services.AddSingleton<HrContractImportRepository>();
        services.AddSingleton<HrCompensationImportRepository>();
        services.AddSingleton<HrInsuranceImportRepository>();
        services.AddSingleton<HrDocumentImportRepository>();
        services.AddSingleton<HrGoLiveConfigurationRepository>();
        services.AddSingleton<HrReminderRepository>();
        services.AddSingleton<HrOffboardingLifecycleRepository>();
        services.AddSingleton<HrHomeRepository>();
        services.AddSingleton<HrTeamRepository>();
        services.AddSingleton<HrReportsRepository>();
        services.AddSingleton<HrReportExportService>();
        services.AddSingleton<WorkCalendarRepository>();
        services.AddSingleton<ShiftRepository>();
        services.AddSingleton<AttendanceRepository>();
        services.AddSingleton<AttendanceImportRepository>();
        services.AddSingleton<HrDocumentRequirementRepository>();
        services.AddSingleton<IHrCountryProviderRegistry, HrCountryProviderRegistry>();
        services.AddSingleton<HrAdjustmentRepository>();
        services.AddSingleton<LeavePolicyRepository>();
        services.AddSingleton<LeaveRequestRepository>();
        services.AddSingleton<OnboardingRepository>();
        services.AddSingleton<OvertimeRepository>();
        services.AddSingleton<PayrollIntegrationRepository>();
        services.AddSingleton<Shared.Contracts.Hr.IHumanResourcesPayrollCapability>(provider => provider.GetRequiredService<PayrollIntegrationRepository>());
        services.AddSingleton<HrService>();
        services.AddSingleton<HrIdentifierCrypto>();
        return services;
    }

    public static WebApplication MapHrModule(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/hr").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/home", async (Guid legalEntityId, DateOnly? asOfDate, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.home.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.GetHomeSummaryAsync(access.Tenant, legalEntityId, date, ct));
        });
        group.MapGet("/reminders", async (Guid legalEntityId, DateOnly? asOfDate, int? page, int? pageSize, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.reminder.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.ListRemindersAsync(access.Tenant, legalEntityId, date, page ?? 1, pageSize ?? 25, ct));
        });
        group.MapGet("/me/team", async (Guid legalEntityId, DateOnly? asOfDate, int? page, int? pageSize, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_team", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.ListMyTeamAsync(access.Tenant, access.MembershipId, legalEntityId, date, page ?? 1, pageSize ?? 25, ct));
        });
        group.MapGet("/me/team/leave-approvals", async (Guid legalEntityId, int? page, int? pageSize, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read_team", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.ListManagerLeaveApprovalsAsync(access.Tenant, access.MembershipId, legalEntityId, date, page ?? 1, pageSize ?? 25, ct));
        });
        group.MapGet("/reports/attendance", async (Guid legalEntityId, DateOnly from, DateOnly to, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.GetAttendanceReportAsync(access.Tenant, legalEntityId, from, to, page ?? 1, pageSize ?? 50, ct));
        });
        group.MapGet("/reports/leave-balances", async (Guid legalEntityId, DateOnly? asOfDate, int? page, int? pageSize, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.GetLeaveBalanceReportAsync(access.Tenant, legalEntityId, date, page ?? 1, pageSize ?? 50, ct));
        });
        group.MapGet("/reports/expiries", async (Guid legalEntityId, DateOnly? asOfDate, int? page, int? pageSize, HttpContext http, HrService service, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await Execute(http, () => service.GetExpiryReportAsync(access.Tenant, legalEntityId, date, page ?? 1, pageSize ?? 50, ct));
        });
        group.MapGet("/reports/attendance/export", async (Guid legalEntityId, DateOnly from, DateOnly to, HttpContext http, HrReportExportService exports, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.export", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            return await ExportFile(http, () => exports.ExportAttendanceAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, from, to, Correlation(http), ct));
        });
        group.MapGet("/reports/leave-balances/export", async (Guid legalEntityId, DateOnly? asOfDate, HttpContext http, HrReportExportService exports, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.export", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await ExportFile(http, () => exports.ExportLeaveBalancesAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, date, Correlation(http), ct));
        });
        group.MapGet("/reports/expiries/export", async (Guid legalEntityId, DateOnly? asOfDate, HttpContext http, HrReportExportService exports, TimeProvider clock, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.report.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.export", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            var date = asOfDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            return await ExportFile(http, () => exports.ExportExpiriesAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, date, Correlation(http), ct));
        });
        group.MapPost("/me/team/leave-approvals/{requestId:guid}/approve", async (Guid requestId, Guid legalEntityId, LeaveRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.approve_team", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.DecideManagerLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, true, Correlation(http), ct));
        });
        group.MapPost("/me/team/leave-approvals/{requestId:guid}/reject", async (Guid requestId, Guid legalEntityId, LeaveRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.approve_team", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { code = "hr.leave_decision_reason_required" });
            return await Execute(http, () => service.DecideManagerLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, false, Correlation(http), ct));
        });
        group.MapPost("/employees", async (CreateEmployeeRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.create", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.LegalEntityId is { } entity && !authorizer.Authorize(access.Subject!, "hr.employment.manage", PermissionScope.ForLegalEntity(access.Tenant, new(entity)))) return Results.Forbid();
            if (request.MembershipId is not null && (access.Subject is null || !authorizer.Authorize(access.Subject, "hr.employee.link_user", PermissionScope.ForTenant(access.Tenant)))) return Results.Forbid();
            return await Execute(http, () => service.CreateEmployeeAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPut("/employees/{employeeId:guid}/membership", async (Guid employeeId, EmployeeMembershipLinkRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_basic", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || access.Subject is null || !authorizer.Authorize(access.Subject, "hr.employee.link_user", PermissionScope.ForTenant(access.Tenant))) return Results.Forbid();
            return await Execute(http, () => service.LinkEmployeeMembershipAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct));
        });
        group.MapGet("/employees", async (string? search, Guid? legalEntityId, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var requestedEntity = legalEntityId ?? accessEntity(http);
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_basic", requestedEntity, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListEmployeesAsync(access.Tenant, new(search, requestedEntity, page ?? 1, pageSize ?? 25), ct));
        });
        group.MapPost("/imports/employees/preview", async (HrEmployeeImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employment.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewEmployeeImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/employees/commit", async (HrEmployeeImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employment.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitEmployeeImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapPost("/imports/assignments/preview", async (HrAssignmentImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employment.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewAssignmentImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/assignments/commit", async (HrAssignmentImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employment.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitAssignmentImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapPost("/imports/contracts/preview", async (HrContractImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.contract.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewContractImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/contracts/commit", async (HrContractImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.contract.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitContractImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapPost("/imports/compensation/preview", async (HrCompensationImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.compensation.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewCompensationImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/compensation/commit", async (HrCompensationImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.compensation.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitCompensationImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapPost("/imports/insurance/preview", async (HrInsuranceImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.insurance.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewInsuranceImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/insurance/commit", async (HrInsuranceImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.insurance.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitInsuranceImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapPost("/imports/documents/preview", async (HrDocumentImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employee.document.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewDocumentImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/documents/commit", async (HrDocumentImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.employee.document.manage", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitDocumentImportAsync(access.Tenant, access.ActorId, access.MembershipId, Correlation(http), request, ct));
        });
        group.MapGet("/legal-entities/{legalEntityId:guid}/go-live", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.golive.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var result = await service.GetGoLiveConfigurationAsync(access.Tenant, legalEntityId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPut("/legal-entities/{legalEntityId:guid}/go-live", async (Guid legalEntityId, HrGoLiveConfigurationRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.golive.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.SaveGoLiveConfigurationAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/me/profile", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_self", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var result = await service.GetMyEmployeeAsync(access.Tenant, access.MembershipId, legalEntityId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/me/documents", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_self", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.employee.document.read_self", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            var profile = await service.GetMyEmployeeAsync(access.Tenant, access.MembershipId, legalEntityId, ct);
            return profile is null ? Results.NotFound() : await Execute(http, () => service.ListDocumentsAsync(access.Tenant, profile.Id, legalEntityId, false, ct));
        });
        group.MapGet("/me/attendance", async (Guid legalEntityId, DateOnly from, DateOnly to, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_self", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.attendance.read_self", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            var profile = await service.GetMyEmployeeAsync(access.Tenant, access.MembershipId, legalEntityId, ct);
            if (profile?.InitialEmployment is not { } employment) return Results.NotFound();
            return await Execute(http, () => service.ListAttendanceAsync(access.Tenant, new(legalEntityId, from, to, employment.Id, null, page ?? 1, pageSize ?? 50), ct));
        });
        group.MapGet("/me/leave/requests", async (Guid legalEntityId, string? status, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_self", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.leave.read_self", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)))) return Results.Forbid();
            var profile = await service.GetMyEmployeeAsync(access.Tenant, access.MembershipId, legalEntityId, ct);
            if (profile?.InitialEmployment is not { } employment) return Results.NotFound();
            return await Execute(http, () => service.ListLeaveRequestsAsync(access.Tenant, legalEntityId, employment.Id, status, page ?? 1, pageSize ?? 25, ct));
        });
        group.MapPost("/me/leave/requests", async (CreateLeaveRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_self", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed || !authorizer.Authorize(access.Subject!, "hr.leave.request_self", PermissionScope.ForLegalEntity(access.Tenant, new(request.LegalEntityId)))) return Results.Forbid();
            var profile = await service.GetMyEmployeeAsync(access.Tenant, access.MembershipId, request.LegalEntityId, ct);
            if (profile?.InitialEmployment is not { Status: EmploymentStatus.Active } employment) return Results.Conflict(new { code = "hr.active_employment_required" });
            return await Execute(http, () => service.CreateLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, employment.Id, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employees/possible-duplicates", async (string arabicName, string? englishName, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_basic", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.FindPossibleDuplicatesAsync(access.Tenant, arabicName, englishName, ct));
        });
        group.MapGet("/employees/{employeeId:guid}", async (Guid employeeId, Guid? legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var requestedEntity = legalEntityId ?? accessEntity(http);
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.read_basic", requestedEntity, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var result = await service.GetEmployeeAsync(access.Tenant, employeeId, requestedEntity, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/employees/{employeeId:guid}/employments", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employment.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListEmploymentsAsync(access.Tenant, employeeId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments", async (Guid employeeId, CreateEmploymentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employment.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateEmploymentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/status", async (Guid employeeId, Guid employmentId, Guid legalEntityId, ChangeEmploymentStatusRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employment.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ChangeEmploymentStatusAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/employees/{employeeId:guid}/employments/{employmentId:guid}/offboarding", async (Guid employeeId, Guid employmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.offboarding.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (await service.GetEmployeeAsync(access.Tenant, employeeId, legalEntityId, ct) is null) return Results.NotFound();
            var result = await service.GetOffboardingCaseAsync(access.Tenant, employeeId, employmentId, legalEntityId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("/offboarding/tasks/{taskId:guid}/complete", async (Guid taskId, Guid legalEntityId, CompleteOffboardingTaskRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.offboarding.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CompleteOffboardingTaskAsync(access.Tenant, access.ActorId, access.MembershipId, taskId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/employees/{employeeId:guid}/identifiers", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.identifier.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListIdentifiersAsync(access.Tenant, employeeId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/identifiers", async (Guid employeeId, CreateEmployeeIdentifierRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.identifier.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateIdentifierAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employees/{employeeId:guid}/identifiers/{identifierId:guid}/reveal", async (Guid employeeId, Guid identifierId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.identifier.reveal", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RevealIdentifierAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, identifierId, legalEntityId, Correlation(http), ct));
        });
        group.MapPost("/employees/{employeeId:guid}/identifiers/{identifierId:guid}/revoke", async (Guid employeeId, Guid identifierId, Guid legalEntityId, RevokeEmployeeIdentifierRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.identifier.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RevokeIdentifierAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, identifierId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/employees/{employeeId:guid}/documents", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var includeSensitive = access.Subject is not null && authorizer.Authorize(access.Subject, "hr.employee.document.read_sensitive", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)));
            return await Execute(http, () => service.ListDocumentsAsync(access.Tenant, employeeId, legalEntityId, includeSensitive, ct));
        });
        group.MapGet("/employees/{employeeId:guid}/documents/{documentId:guid}/download", async (Guid employeeId, Guid documentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var includeSensitive = access.Subject is not null && authorizer.Authorize(access.Subject, "hr.employee.document.read_sensitive", PermissionScope.ForLegalEntity(access.Tenant, new(legalEntityId)));
            return await Execute(http, () => service.CreateDocumentDownloadAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, documentId, legalEntityId, includeSensitive, Correlation(http), ct));
        });
        group.MapPost("/employees/{employeeId:guid}/documents/upload-intents", async (Guid employeeId, CreateEmployeeDocumentUploadRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateDocumentUploadIntentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employees/{employeeId:guid}/documents/uploads/{fileRef:guid}/complete", async (Guid employeeId, Guid fileRef, CompleteEmployeeDocumentUploadRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CompleteDocumentUploadAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request.LegalEntityId, request.EmploymentId, fileRef, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/documents", async (Guid employeeId, CreateEmployeeDocumentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateDocumentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employees/{employeeId:guid}/document-completeness", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.GetDocumentCompletenessAsync(access.Tenant, employeeId, legalEntityId, ct));
        });
        group.MapGet("/document-requirements", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListRequiredDocumentRulesAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPut("/document-requirements", async (SaveRequiredDocumentRuleRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.SaveRequiredDocumentRuleAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct));
        });
        group.MapPost("/employees/{employeeId:guid}/documents/{documentId:guid}/verify", async (Guid employeeId, Guid documentId, Guid legalEntityId, VerifyEmployeeDocumentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.document.verify", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.VerifyDocumentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, documentId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/employees/{employeeId:guid}/timeline", async (Guid employeeId, Guid? legalEntityId, int? limit, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var requestedEntity = legalEntityId ?? accessEntity(http);
            var access = await AccessAsync(http, subjects, authorizer, "hr.timeline.read", requestedEntity, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.GetTimelineAsync(access.Tenant, employeeId, requestedEntity, limit ?? 50, ct));
        });
        group.MapGet("/departments", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.department.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListDepartmentsAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPost("/departments", async (CreateDepartmentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.department.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateDepartmentAsync(access.Tenant, access.ActorId, request.LegalEntityId, request, ct), StatusCodes.Status201Created);
        });
        group.MapPost("/departments/{id:guid}/archive", async (Guid id, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.department.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ArchiveDepartmentAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId, Correlation(http), ct));
        });
        group.MapGet("/positions", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.position.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListPositionsAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPost("/positions", async (CreatePositionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.position.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreatePositionAsync(access.Tenant, access.ActorId, request.LegalEntityId, request, ct), StatusCodes.Status201Created);
        });
        group.MapPost("/positions/{id:guid}/archive", async (Guid id, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.position.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ArchivePositionAsync(access.Tenant, access.ActorId, access.MembershipId, id, legalEntityId, Correlation(http), ct));
        });
        group.MapPost("/employments/{employmentId:guid}/assignments", async (Guid employmentId, CreateAssignmentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.assignment.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateAssignmentAsync(access.Tenant, access.ActorId, access.MembershipId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employments/{employmentId:guid}/assignments", async (Guid employmentId, Guid legalEntityId, DateOnly? asOf, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.assignment.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListAssignmentsAsync(access.Tenant, employmentId, legalEntityId, asOf, ct));
        });
        group.MapGet("/employments/{employmentId:guid}/contracts", async (Guid employmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListContractsAsync(access.Tenant, employmentId, legalEntityId, ct));
        });
        group.MapPost("/employments/{employmentId:guid}/contracts", async (Guid employmentId, CreateContractRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateContractAsync(access.Tenant, access.ActorId, access.MembershipId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/contracts/{contractId:guid}/activate", async (Guid contractId, Guid legalEntityId, ActivateContractRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ActivateContractAsync(access.Tenant, access.ActorId, access.MembershipId, contractId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapPost("/contracts/{contractId:guid}/end", async (Guid contractId, Guid legalEntityId, EndContractRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.EndContractAsync(access.Tenant, access.ActorId, access.MembershipId, contractId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/contracts/{contractId:guid}/amendments", async (Guid contractId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListContractAmendmentsAsync(access.Tenant, contractId, legalEntityId, ct));
        });
        group.MapPost("/contracts/{contractId:guid}/amendments", async (Guid contractId, Guid legalEntityId, CreateContractAmendmentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.contract.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateContractAmendmentAsync(access.Tenant, access.ActorId, access.MembershipId, contractId, legalEntityId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employees/{employeeId:guid}/employments/{employmentId:guid}/insurance", async (Guid employmentId, Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.insurance.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListInsuranceEnrollmentsAsync(access.Tenant, employeeId, employmentId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/insurance", async (Guid employeeId, Guid employmentId, CreateInsuranceEnrollmentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.insurance.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateInsuranceEnrollmentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/insurance/{enrollmentId:guid}/activate", async (Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.insurance.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ActivateInsuranceEnrollmentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/insurance/{enrollmentId:guid}/reject", async (Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.insurance.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RejectInsuranceEnrollmentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/insurance/{enrollmentId:guid}/reveal", async (Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.insurance.reveal", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RevealInsuranceNumberAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, enrollmentId, legalEntityId, Correlation(http), ct));
        });
        group.MapGet("/employments/{employmentId:guid}/compensation", async (Guid employmentId, Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.compensation.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListCompensationAgreementsAsync(access.Tenant, employeeId, employmentId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/compensation", async (Guid employeeId, Guid employmentId, CreateCompensationAgreementRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.compensation.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateCompensationAgreementAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employees/{employeeId:guid}/qualifications", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.qualification.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListQualificationsAsync(access.Tenant, employeeId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/qualifications", async (Guid employeeId, CreateQualificationRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.qualification.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateQualificationAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employees/{employeeId:guid}/credentials", async (Guid employeeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.credential.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListCredentialsAsync(access.Tenant, employeeId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/credentials", async (Guid employeeId, CreateCredentialRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.credential.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateCredentialAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employees/{employeeId:guid}/credentials/{credentialId:guid}/verify", async (Guid employeeId, Guid credentialId, Guid legalEntityId, VerifyCredentialRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.credential.verify", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.VerifyCredentialAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, credentialId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/work-calendars", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListWorkCalendarsAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPost("/work-calendars", async (CreateWorkCalendarRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateWorkCalendarAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/work-calendars/{calendarId:guid}/holidays", async (Guid calendarId, Guid legalEntityId, AddWorkCalendarHolidayRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.AddWorkCalendarHolidayAsync(access.Tenant, access.ActorId, access.MembershipId, calendarId, legalEntityId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/work-calendars/{calendarId:guid}/holidays", async (Guid calendarId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListWorkCalendarHolidaysAsync(access.Tenant, calendarId, legalEntityId, ct));
        });
        group.MapPost("/work-calendar-holidays/{holidayId:guid}/cancel", async (Guid holidayId, Guid legalEntityId, CancelWorkCalendarHolidayRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CancelWorkCalendarHolidayAsync(access.Tenant, access.ActorId, access.MembershipId, holidayId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/employments/{employmentId:guid}/work-day", async (Guid employmentId, Guid legalEntityId, DateOnly date, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.calendar.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            var result = await service.ResolveWorkDayAsync(access.Tenant, employmentId, legalEntityId, date, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("/employments/{employmentId:guid}/attendance/punches", async (Guid employmentId, CreateAttendancePunchRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RecordAttendancePunchAsync(access.Tenant, access.ActorId, access.MembershipId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/attendance/imports/preview", async (HrAttendanceImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.PreviewAttendanceImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/attendance/imports/commit", async (HrAttendanceImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CommitAttendanceImportAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct));
        });
        group.MapGet("/attendance-records", async (Guid legalEntityId, DateOnly from, DateOnly to, Guid? employmentId, string? status, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListAttendanceAsync(access.Tenant, new(legalEntityId, from, to, employmentId, status, page ?? 1, pageSize ?? 50), ct));
        });
        group.MapPost("/attendance-records/{recordId:guid}/corrections", async (Guid recordId, Guid legalEntityId, AttendanceCorrectionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.correct", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CorrectAttendanceAsync(access.Tenant, access.ActorId, access.MembershipId, recordId, legalEntityId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/attendance-records/{recordId:guid}/corrections", async (Guid recordId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListAttendanceCorrectionsAsync(access.Tenant, recordId, legalEntityId, ct));
        });
        group.MapGet("/attendance-exceptions", async (Guid legalEntityId, string? status, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListAttendanceExceptionsAsync(access.Tenant, legalEntityId, status, page ?? 1, pageSize ?? 50, ct));
        });
        group.MapGet("/shifts", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.shift.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListShiftDefinitionsAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPost("/shifts", async (CreateShiftDefinitionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.shift.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateShiftDefinitionAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/employments/{employmentId:guid}/shift-assignments", async (Guid employmentId, AssignShiftRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.shift.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.AssignShiftAsync(access.Tenant, access.ActorId, access.MembershipId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employments/{employmentId:guid}/shift-assignments", async (Guid employmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.shift.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListShiftAssignmentsAsync(access.Tenant, employmentId, legalEntityId, ct));
        });
        group.MapGet("/employments/{employmentId:guid}/adjustments", async (Guid employmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListHrAdjustmentsAsync(access.Tenant, employmentId, legalEntityId, ct));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/adjustments", async (Guid employeeId, Guid employmentId, CreateHrAdjustmentRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateHrAdjustmentAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/adjustments/{adjustmentId:guid}/submit", async (Guid adjustmentId, Guid legalEntityId, HrAdjustmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.SubmitHrAdjustmentAsync(access.Tenant, access.ActorId, access.MembershipId, adjustmentId, legalEntityId, request.ExpectedVersion, Correlation(http), ct));
        });
        group.MapPost("/adjustments/{adjustmentId:guid}/approve", async (Guid adjustmentId, Guid legalEntityId, HrAdjustmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.approve", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ApproveHrAdjustmentAsync(access.Tenant, access.ActorId, access.MembershipId, adjustmentId, legalEntityId, request.ExpectedVersion, Correlation(http), ct));
        });
        group.MapPost("/adjustments/{adjustmentId:guid}/reject", async (Guid adjustmentId, Guid legalEntityId, HrAdjustmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.approve", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.RejectHrAdjustmentAsync(access.Tenant, access.ActorId, access.MembershipId, adjustmentId, legalEntityId, request.ExpectedVersion, request.Reason ?? string.Empty, Correlation(http), ct));
        });
        group.MapPost("/adjustments/{adjustmentId:guid}/cancel", async (Guid adjustmentId, Guid legalEntityId, HrAdjustmentDecisionRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.adjustment.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CancelHrAdjustmentAsync(access.Tenant, access.ActorId, access.MembershipId, adjustmentId, legalEntityId, request.ExpectedVersion, request.Reason ?? string.Empty, Correlation(http), ct));
        });
        group.MapGet("/leave/types", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListLeaveTypesAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapGet("/leave/country-providers/{providerCode}/templates", async (string providerCode, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => Task.FromResult(service.GetCountryLeaveTemplates(providerCode)));
        });
        group.MapPost("/leave/types", async (CreateLeaveTypeRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateLeaveTypeAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/leave/types/{leaveTypeId:guid}/policies", async (Guid leaveTypeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListLeavePoliciesAsync(access.Tenant, legalEntityId, leaveTypeId, ct));
        });
        group.MapPost("/leave/policies", async (CreateLeavePolicyRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateLeavePolicyAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/leave/requests", async (Guid legalEntityId, Guid? employmentId, string? status, int page, int pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListLeaveRequestsAsync(access.Tenant, legalEntityId, employmentId, status, page, pageSize, ct));
        });
        group.MapPost("/employments/{employmentId:guid}/leave-requests", async (Guid employmentId, CreateLeaveRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.manage", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/leave/requests/{requestId:guid}/approve", async (Guid requestId, Guid legalEntityId, LeaveRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.approve", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.DecideLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, true, Correlation(http), ct));
        });
        group.MapPost("/leave/requests/{requestId:guid}/reject", async (Guid requestId, Guid legalEntityId, LeaveRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.approve", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { code = "hr.leave_decision_reason_required" });
            return await Execute(http, () => service.DecideLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, false, Correlation(http), ct));
        });
        group.MapPost("/leave/requests/{requestId:guid}/cancel", async (Guid requestId, Guid legalEntityId, LeaveRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { code = "hr.leave_decision_reason_required" });
            return await Execute(http, () => service.DecideLeaveRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, false, Correlation(http), ct));
        });
        group.MapGet("/employments/{employmentId:guid}/leave-types/{leaveTypeId:guid}/balance", async (Guid employmentId, Guid leaveTypeId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.read", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.GetLeaveBalanceAsync(access.Tenant, employmentId, leaveTypeId, legalEntityId, ct));
        });
        group.MapPost("/leave/balance-adjustments", async (AdjustLeaveBalanceRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.balance.adjust", request.LegalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.AdjustLeaveBalanceAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/imports/leave-openings/preview", async (HrLeaveOpeningImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.leave.balance.adjust", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.PreviewLeaveOpeningImportAsync(access.Tenant, request, ct));
        });
        group.MapPost("/imports/leave-openings/commit", async (HrLeaveOpeningImportRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.employee.import", null, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            if (request.Rows is null || request.Rows.Any(row => row is null || row.LegalEntityId == Guid.Empty)) return Results.BadRequest(new { code = "hr.import_rows_invalid" });
            if (request.Rows.Any(row => !authorizer.Authorize(access.Subject!, "hr.leave.balance.adjust", PermissionScope.ForLegalEntity(access.Tenant, new(row.LegalEntityId))))) return Results.Forbid();
            return await Execute(http, () => service.CommitLeaveOpeningImportAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct));
        });
        group.MapPost("/leave/accrual-runs", async (Guid legalEntityId, AccrueLeaveRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.leave.manage", legalEntityId, ct);
            if (access is null) return Results.Unauthorized();
            if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.AccrueLeaveAsync(access.Tenant, access.ActorId, access.MembershipId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/onboarding/templates", async (Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.onboarding.read", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListOnboardingTemplatesAsync(access.Tenant, legalEntityId, ct));
        });
        group.MapPost("/onboarding/templates", async (CreateOnboardingTemplateRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.onboarding.manage", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateOnboardingTemplateAsync(access.Tenant, access.ActorId, access.MembershipId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapGet("/employments/{employmentId:guid}/onboarding", async (Guid employmentId, Guid legalEntityId, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.onboarding.read", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, async () => await service.GetOnboardingCaseAsync(access.Tenant, employmentId, legalEntityId, ct) ?? throw new HrException("hr.onboarding_case_not_found"));
        });
        group.MapPost("/employees/{employeeId:guid}/employments/{employmentId:guid}/onboarding", async (Guid employeeId, Guid employmentId, CreateOnboardingCaseRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.onboarding.manage", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateOnboardingCaseAsync(access.Tenant, access.ActorId, access.MembershipId, employeeId, employmentId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/onboarding/tasks/{taskId:guid}/complete", async (Guid taskId, Guid legalEntityId, CompleteOnboardingTaskRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.onboarding.manage", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CompleteOnboardingTaskAsync(access.Tenant, access.ActorId, access.MembershipId, taskId, legalEntityId, request, Correlation(http), ct));
        });
        group.MapGet("/overtime-requests", async (Guid legalEntityId, string? status, int? page, int? pageSize, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.read", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.ListOvertimeRequestsAsync(access.Tenant, legalEntityId, status, page ?? 1, pageSize ?? 50, ct));
        });
        group.MapPost("/attendance-records/{recordId:guid}/overtime-requests", async (Guid recordId, CreateOvertimeRequest request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.manage", request.LegalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.CreateOvertimeRequestAsync(access.Tenant, access.ActorId, access.MembershipId, recordId, request, Correlation(http), ct), StatusCodes.Status201Created);
        });
        group.MapPost("/overtime-requests/{requestId:guid}/approve", async (Guid requestId, Guid legalEntityId, OvertimeRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.overtime.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            return await Execute(http, () => service.DecideOvertimeRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, true, Correlation(http), ct));
        });
        group.MapPost("/overtime-requests/{requestId:guid}/reject", async (Guid requestId, Guid legalEntityId, OvertimeRequestDecision request, HttpContext http, HrService service, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, CancellationToken ct) =>
        {
            var access = await AccessAsync(http, subjects, authorizer, "hr.attendance.overtime.approve", legalEntityId, ct); if (access is null) return Results.Unauthorized(); if (!access.Allowed) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { code = "hr.overtime_decision_reason_required" });
            return await Execute(http, () => service.DecideOvertimeRequestAsync(access.Tenant, access.ActorId, access.MembershipId, requestId, legalEntityId, request, false, Correlation(http), ct));
        });
        return app;
    }

    private static Guid? accessEntity(HttpContext http) => Guid.TryParse(http.User.FindFirstValue("legal_entity_id"), out var entity) ? entity : null;

    private static async Task<Access?> AccessAsync(HttpContext http, IAuthorizationSubjectStore subjects, IPermissionAuthorizer authorizer, string permission, Guid? legalEntityId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("tenant_id"), out var tenantId)
            || !Guid.TryParse(http.User.FindFirstValue("membership_id"), out var membershipId)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return null;
        var claimEntity = accessEntity(http);
        if (claimEntity is not null && legalEntityId is not null && claimEntity != legalEntityId) return new(new(tenantId), actorId, membershipId, false, null!);
        var tenant = new TenantId(tenantId);
        var subject = await subjects.LoadAsync(tenant, membershipId, ct);
        if (subject is null) return null;
        var scope = legalEntityId is { } entity ? PermissionScope.ForLegalEntity(tenant, new(entity)) : PermissionScope.ForTenant(tenant);
        return new(tenant, actorId, membershipId, authorizer.Authorize(subject, permission, scope), subject);
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Func<Task<T>> action, int status = StatusCodes.Status200OK)
    {
        try
        {
            var result = await action();
            return status == StatusCodes.Status201Created ? Results.Created(http.Request.Path, result) : Results.Ok(result);
        }
        catch (HrException exception)
        {
            return MapException(exception);
        }
        catch (Exception exception)
        {
            var translated = HrService.TranslatePersistenceException(exception);
            if (translated is null) throw;
            return MapException(translated);
        }
    }

    private static async Task<IResult> ExportFile(HttpContext http, Func<Task<HrReportExport>> action)
    {
        try
        {
            var file = await action();
            return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
        }
        catch (HrException exception) { return MapException(exception); }
        catch (Exception exception)
        {
            var translated = HrService.TranslatePersistenceException(exception);
            if (translated is null) throw;
            return MapException(translated);
        }
    }

    private static IResult MapException(HrException exception)
    {
        if (exception.Code == "hr.manager_relationship_required") return Results.Forbid();
        if (exception.Code is "hr.document_not_found" or "hr.document_file_unavailable" or "hr.document_upload_not_found") return Results.NotFound();
        if (exception.Code == "hr.document_upload_unavailable" || exception.Code == "hr.file_access_unavailable") return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        if (exception.Code == "hr.document_not_downloadable") return Results.Conflict();
        var conflict = exception.Code.Contains("duplicate", StringComparison.Ordinal) || exception.Code.Contains("overlap", StringComparison.Ordinal) || exception.Code.Contains("concurrency", StringComparison.Ordinal) || exception.Code.Contains("in_use", StringComparison.Ordinal) || exception.Code.Contains("already_linked", StringComparison.Ordinal);
        var badRequest = exception.Code.Contains("reference_invalid", StringComparison.Ordinal) || exception.Code.Contains("business_rule_violation", StringComparison.Ordinal) || exception.Code.Contains("hierarchy_cycle", StringComparison.Ordinal) || exception.Code is "hr.document_upload_invalid" or "hr.document_metadata_invalid";
        var status = exception.Code == "hr.export_row_limit_exceeded" ? StatusCodes.Status413PayloadTooLarge : badRequest ? StatusCodes.Status400BadRequest : conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
        return Results.Problem(statusCode: status, title: exception.Code, extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
    }

    private static Guid Correlation(HttpContext http) => Guid.TryParse(http.Request.Headers["X-Correlation-Id"], out var value) ? value : Guid.CreateVersion7();

    private sealed record Access(TenantId Tenant, Guid ActorId, Guid MembershipId, bool Allowed, Core.Authorization.AuthorizationSubject? Subject);
}

public sealed class HrModuleManifestProvider : IModuleManifestProvider
{
    public ModuleManifest Manifest { get; } = new("hr", "1.0.0", 2, "L4", "business-module", true, "tenant",
        new(["platform-kernel"], ["platform.files", "platform.approvals", "platform.notifications", "platform.scheduler", "platform.outbox", "platform.iam", "directory.party"]),
        new("business_hr", ["employee", "employment", "employee_import_reference", "legal_entity_go_live_configuration", "department", "position", "employment_assignment", "employment_assignment_import_reference", "work_calendar", "work_calendar_weekly_pattern", "work_calendar_holiday", "shift_definition", "shift_assignment", "attendance_punch", "attendance_import_reference", "attendance_record", "attendance_exception", "attendance_correction", "attendance_overtime_request", "attendance_overtime_event", "payroll_period_snapshot", "attendance_payroll_lock", "attendance_payroll_correction_delta", "leave_type", "leave_policy", "leave_balance_movement", "leave_import_reference", "leave_request", "leave_request_event", "leave_request_charge", "onboarding_template", "onboarding_template_task", "onboarding_case", "onboarding_task", "onboarding_task_event", "employment_contract", "employment_contract_import_reference", "employment_contract_amendment", "identifier_lookup_key", "employee_identifier", "employee_document", "employee_document_requirement", "employee_document_import_reference", "employee_qualification", "employee_credential", "employee_insurance_enrollment", "employee_insurance_import_reference", "employment_compensation_agreement", "employment_compensation_import_reference", "hr_adjustment", "employment_event", "employee_list_projection", "employee_timeline_projection"]),
        ["hr.employee.read_basic", "hr.employee.create", "hr.employee.import", "hr.employee.manage", "hr.employee.archive", "hr.employee.link_user", "hr.employee.read_personal", "hr.employee.read_self", "hr.employee.read_team", "hr.employee.identifier.read", "hr.employee.identifier.manage", "hr.employee.identifier.reveal", "hr.employee.document.read", "hr.employee.document.read_self", "hr.employee.document.read_sensitive", "hr.employee.document.manage", "hr.employee.document.verify", "hr.employee.qualification.read", "hr.employee.qualification.manage", "hr.employee.credential.read", "hr.employee.credential.manage", "hr.employee.credential.verify", "hr.insurance.read", "hr.insurance.manage", "hr.insurance.reveal", "hr.compensation.read", "hr.compensation.manage", "hr.golive.manage", "hr.calendar.read", "hr.calendar.manage", "hr.shift.read", "hr.shift.manage", "hr.adjustment.read", "hr.adjustment.manage", "hr.adjustment.approve", "hr.leave.read", "hr.leave.read_self", "hr.leave.read_team", "hr.leave.approve_team", "hr.leave.request_self", "hr.leave.manage", "hr.leave.approve", "hr.leave.balance.adjust", "hr.attendance.read", "hr.attendance.read_self", "hr.attendance.manage", "hr.attendance.correct", "hr.attendance.overtime.approve", "hr.attendance.payroll_lock", "hr.onboarding.read", "hr.onboarding.manage", "hr.employment.read", "hr.employment.manage", "hr.assignment.read", "hr.assignment.manage", "hr.contract.read", "hr.contract.manage", "hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.timeline.read", "hr.audit.read", "hr.export", "hr.home.read", "hr.report.read"],
        new(["hr.core", "hr.contracts", "hr.assignments", "hr.timeline", "hr.adjustments", "hr.shifts", "hr.attendance", "hr.leave-policies", "hr.leave-balances", "hr.leave-requests", "hr.payroll-input"], ["platform.organization", "platform.iam", "platform.audit", "platform.transaction-coordination", "platform.numbering", "platform.files"]),
        new([], []), new(true, "blocked_if_dependents"));
}
