using Core.Transactions;
using Core.Services;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Tests.Modules.Hr;

public sealed class HrImportValidationTests
{
    [Fact]
    public async Task EmployeeImportRejectsMissingFileHashBeforeOpeningDatabaseConnection()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var repository = new HrImportRepository(new NpgsqlTransactionCoordinator(dataSource), new UnusedSequenceAllocator(), TimeProvider.System);
        var request = new HrEmployeeImportRequest("LegacyHR", "batch-01", "", [
            new HrEmployeeImportRow("row-1", "emp-1", "أحمد علي", null, null, Guid.CreateVersion7(), EmploymentType.FullTime,
                new DateOnly(2024, 1, 1), null, EmploymentStatus.Active, "Complete")
        ]);

        await Assert.ThrowsAsync<HrException>(() => repository.CommitEmployeesAsync(new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), request, CancellationToken.None));
    }

    [Fact]
    public async Task EmployeeImportPreviewRejectsManagerCyclesBeforeOpeningDatabaseConnection()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1");
        var repository = new HrImportRepository(new NpgsqlTransactionCoordinator(dataSource), new UnusedSequenceAllocator(), TimeProvider.System);
        var legalEntityId = Guid.CreateVersion7();
        var request = new HrEmployeeImportRequest("LegacyHR", "batch-02", new string('a', 64), [
            new HrEmployeeImportRow("row-1", "emp-1", "أحمد علي", null, null, legalEntityId, EmploymentType.FullTime,
                new DateOnly(2024, 1, 1), null, EmploymentStatus.Active, "Complete", ManagerLegacyEmployeeId: "emp-2"),
            new HrEmployeeImportRow("row-2", "emp-2", "سارة علي", null, null, legalEntityId, EmploymentType.FullTime,
                new DateOnly(2024, 1, 1), null, EmploymentStatus.Active, "Complete", ManagerLegacyEmployeeId: "emp-1")
        ]);

        var summary = await repository.PreviewEmployeesAsync(new TenantId(Guid.CreateVersion7()), request, CancellationToken.None);

        Assert.Equal(2, summary.RejectedRows);
        Assert.All(summary.Rows, row => Assert.Contains("hr.import_manager_cycle", row.Issues));
    }

    private sealed class UnusedSequenceAllocator : ITransactionalSequenceAllocator
    {
        public Task<string> AllocateAsync(ITransactionContext context, SequenceDefinition definition, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Validation should reject the request before sequence allocation.");
    }
}
