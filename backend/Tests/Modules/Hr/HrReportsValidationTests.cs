using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Tests.Modules.Hr;

public sealed class HrReportsValidationTests
{
    [Fact]
    public async Task AttendanceReportRejectsMoreThanOneYearBeforeOpeningDatabaseConnection()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var repository = new HrReportsRepository(dataSource, TimeProvider.System);
        var tenant = new TenantId(Guid.CreateVersion7());
        var from = new DateOnly(2025, 1, 1);
        var to = from.AddDays(367);

        await Assert.ThrowsAsync<HrException>(() => repository.GetAttendanceReportAsync(tenant, Guid.CreateVersion7(), from, to, 1, 50, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiryReportRejectsFutureAsOfDateBeforeOpeningDatabaseConnection()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var repository = new HrReportsRepository(dataSource, TimeProvider.System);

        await Assert.ThrowsAsync<HrException>(() => repository.GetExpiryReportAsync(
            new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), 1, 50, CancellationToken.None));
    }
}
