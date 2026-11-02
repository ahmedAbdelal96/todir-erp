using System.Globalization;
using System.Text;
using Core.Services;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Services;

public sealed record HrReportExport(string FileName, byte[] Content);

public sealed class HrReportExportService(HrReportsRepository reports, IAuditWriter audit, TimeProvider clock)
{
    private const int MaximumRows = 10_000;

    public Task<HrReportExport> ExportAttendanceAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, DateOnly from, DateOnly to, Guid correlationId, CancellationToken ct) =>
        ExportAsync(tenant, actorId, membershipId, legalEntityId, correlationId, "attendance", $"attendance-{from:yyyyMMdd}-{to:yyyyMMdd}.csv",
            page => reports.GetAttendanceReportAsync(tenant, legalEntityId, from, to, page, 100, ct),
            page => page.TotalCount, page => page.Items, AttendanceHeader,
            row => [Csv(row.EmploymentNumber), Csv(row.EmployeeName), Number(row.RecordCount), Number(row.PresentDays), Number(row.AbsentDays), Number(row.PartialDays), Number(row.ApprovedLeaveDays), Number(row.WorkedMinutes), Number(row.LateMinutes), Number(row.OvertimeEligibleMinutes), Number(row.ApprovedOvertimeMinutes), Number(row.OpenExceptionCount)], ct);

    public Task<HrReportExport> ExportLeaveBalancesAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, Guid correlationId, CancellationToken ct) =>
        ExportAsync(tenant, actorId, membershipId, legalEntityId, correlationId, "leave-balances", $"leave-balances-{asOfDate:yyyyMMdd}.csv",
            page => reports.GetLeaveBalanceReportAsync(tenant, legalEntityId, asOfDate, page, 100, ct),
            page => page.TotalCount, page => page.Items, LeaveBalanceHeader,
            row => [Csv(row.EmploymentNumber), Csv(row.EmployeeName), Csv(row.LeaveTypeCode), Csv(row.LeaveTypeNameAr), Csv(row.Unit), Number(row.Balance)], ct);

    public Task<HrReportExport> ExportExpiriesAsync(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, DateOnly asOfDate, Guid correlationId, CancellationToken ct) =>
        ExportAsync(tenant, actorId, membershipId, legalEntityId, correlationId, "expiries", $"hr-expiries-{asOfDate:yyyyMMdd}.csv",
            page => reports.GetExpiryReportAsync(tenant, legalEntityId, asOfDate, page, 100, ct),
            page => page.TotalCount, page => page.Items, ExpiryHeader,
            row => [Csv(row.EmploymentNumber ?? string.Empty), Csv(row.EmployeeName), Csv(row.ItemType), Csv(row.ItemLabel), row.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Number(row.DaysFromAsOfDate)], ct);

    private async Task<HrReportExport> ExportAsync<TPage, TRow>(TenantId tenant, Guid actorId, Guid membershipId, Guid legalEntityId, Guid correlationId, string reportKey, string fileName,
        Func<int, Task<TPage>> fetch, Func<TPage, int> total, Func<TPage, IReadOnlyList<TRow>> rows, string header, Func<TRow, string[]> serialize, CancellationToken ct)
    {
        var first = await fetch(1);
        var rowCount = total(first);
        if (rowCount > MaximumRows) throw new HrException("hr.export_row_limit_exceeded");
        var builder = new StringBuilder(Math.Max(128, rowCount * 96));
        builder.AppendLine(header);
        AppendRows(builder, rows(first), serialize);
        var pageCount = (rowCount + 99) / 100;
        for (var pageNumber = 2; pageNumber <= pageCount; pageNumber++)
        {
            var page = await fetch(pageNumber);
            AppendRows(builder, rows(page), serialize);
        }
        var now = clock.GetUtcNow();
        await audit.WriteAsync(new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "User", actorId, membershipId, "hr", "hr.report.exported", "hr_report", legalEntityId,
            null, System.Text.Json.JsonSerializer.Serialize(new { reportKey, rowCount, fileName }), "Authorized report export", "api", correlationId, null, now, null), ct);
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(builder.ToString());
        var content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);
        return new(fileName, content);
    }

    private static void AppendRows<TRow>(StringBuilder builder, IReadOnlyList<TRow> rows, Func<TRow, string[]> serialize)
    {
        foreach (var row in rows) builder.AppendLine(string.Join(',', serialize(row)));
    }

    private static string Csv(string value)
    {
        var firstMeaningfulCharacter = value.AsSpan().TrimStart();
        var safe = firstMeaningfulCharacter.Length > 0 && firstMeaningfulCharacter[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Number<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);

    private const string AttendanceHeader = "رقم التوظيف,الموظف,السجلات,أيام حضور,أيام غياب,أيام جزئية,إجازة معتمدة,دقائق العمل,دقائق التأخير,دقائق إضافية مستحقة,إضافي معتمد,استثناءات مفتوحة";
    private const string LeaveBalanceHeader = "رقم التوظيف,الموظف,نوع الإجازة,الاسم,الوحدة,الرصيد";
    private const string ExpiryHeader = "رقم التوظيف,الموظف,نوع السجل,الوصف,تاريخ الانتهاء,الأيام المتبقية";
}
