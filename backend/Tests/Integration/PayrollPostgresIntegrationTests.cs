using Modules.Payroll.DTOs;
using Modules.Payroll.Repositories;
using Modules.Payroll.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PayrollPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    [PayrollPostgresFact]
    public async Task ImportRequiresExplicitApprovalAndOpeningLoanFeedsRecoverySchedule()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var source = "payroll-acceptance-" + Guid.CreateVersion7().ToString("N");
        var batchId = Guid.Empty;
        var inputId = Guid.Empty;
        var openingId = Guid.Empty;
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new PayrollRepository(dataSource, new PayrollCountryProviderRegistry());
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            var input = new PayrollImportRequest("Acceptance", source, source, source,
                [new(1, "transport-1", 725m, "EGP", fixture.EmploymentId, "TRANSPORT", new DateOnly(2026, 10, 15))]);
            var imported = await repository.ImportInputsAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), input, true, default);
            batchId = imported.BatchId;
            inputId = imported.Rows.Single().InputId!.Value;
            Assert.Equal("Draft", (await repository.GetInputAsync(tenant, fixture.LegalEntityId, inputId, default))!.Status);
            await repository.ApproveInputAsync(tenant, fixture.LegalEntityId, inputId, default);
            var replay = await repository.ImportInputsAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(), input, true, default);
            Assert.Equal("Committed", replay.Status);
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.payroll_input where tenant_id=$1 and idempotency_key=$2", fixture.TenantId, $"payroll-import:{batchId:D}:transport-1"));

            var opening = await repository.CreateOpeningBalanceAsync(tenant, fixture.LegalEntityId, Guid.CreateVersion7(),
                new(fixture.EmploymentId, "Loan", 1000m, "EGP", new DateOnly(2026, 9, 30), source, 250m, 4), default);
            openingId = opening.Id;
            Assert.Equal("Approved", (await repository.ApproveOpeningBalanceAsync(tenant, fixture.LegalEntityId, openingId, default)).Status);
            Assert.Equal(1L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.employee_receivable where tenant_id=$1 and employment_id=$2 and principal=1000 and created_at >= (select created_at from business_payroll.payroll_opening_balance where id=$3)", fixture.TenantId, fixture.EmploymentId, openingId));
            Assert.Equal(4L, await CountAsync(adminConnectionString, "select count(*) from business_payroll.recovery_installment i join business_payroll.employee_receivable r on r.id=i.receivable_id where i.tenant_id=$1 and r.employment_id=$2 and r.principal=1000 and r.created_at >= (select created_at from business_payroll.payroll_opening_balance where id=$3)", fixture.TenantId, fixture.EmploymentId, openingId));
            Assert.Single(await repository.ListOpeningBalancesAsync(tenant, fixture.LegalEntityId, 1, 10, fixture.EmploymentId, "Loan", source, default));
        }
        finally
        {
            await CleanupAsync(adminConnectionString, fixture.TenantId, batchId, inputId, openingId);
        }
    }

    private static async Task<PayrollFixture> ReadFixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select t.id,e.id,em.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id and e.status='Active' join business_hr.employment em on em.tenant_id=t.id and em.legal_entity_id=e.id and em.status='Active' where t.slug='atlas-trading' order by em.employment_number limit 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical development seed must contain Atlas active payroll data.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }

    private static async Task<long> CountAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection); Add(command, values);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task CleanupAsync(string connectionString, Guid tenantId, Guid batchId, Guid inputId, Guid openingId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var sql in new[]
        {
            "delete from business_payroll.recovery_installment where tenant_id=$1 and receivable_id in (select id from business_payroll.employee_receivable where tenant_id=$1 and created_at >= (select created_at from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2))",
            "delete from business_payroll.employee_receivable where tenant_id=$1 and created_at >= (select created_at from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2)",
            "delete from business_payroll.payroll_opening_balance where tenant_id=$1 and id=$2",
            "delete from business_payroll.payroll_input where tenant_id=$1 and id=$2",
            "delete from business_payroll.payroll_import_row where tenant_id=$1 and batch_id=$2",
            "delete from business_payroll.payroll_import_batch where tenant_id=$1 and id=$2"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue(tenantId); if (sql.Contains("$2")) command.Parameters.AddWithValue(sql.Contains("opening_balance") || sql.Contains("receivable") ? openingId : sql.Contains("payroll_input") ? inputId : batchId); await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static void Add(NpgsqlCommand command, params object[] values) { foreach (var value in values) command.Parameters.AddWithValue(value); }
    private sealed record PayrollFixture(Guid TenantId, Guid LegalEntityId, Guid EmploymentId);

    private sealed class PayrollPostgresFactAttribute : FactAttribute
    {
        public PayrollPostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TODIR_PAYROLL_TEST_DATABASE") != "1") Skip = "Set TODIR_PAYROLL_TEST_DATABASE=1 only for the disposable PostgreSQL payroll integration gate."; }
    }
}
