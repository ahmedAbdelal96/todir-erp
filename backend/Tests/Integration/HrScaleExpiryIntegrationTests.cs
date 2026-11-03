using Core.Transactions;
using Modules.Hr.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;
using Xunit.Abstractions;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrScaleExpiryIntegrationTests(ITestOutputHelper output)
{
    [HrScaleExpiryFact]
    public async Task CarryoverExpiryProcessesLargeBoundedSetAndPreservesFutureEntitlements()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the local HR expiry scale test.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION to the non-owner erp_app role for the local HR expiry scale test.");
        var databaseName = new NpgsqlConnectionStringBuilder(adminConnectionString).Database;
        Assert.StartsWith("todir_hr_perf_", databaseName, StringComparison.Ordinal);

        var asOfDate = new DateOnly(2025, 1, 1);
        var fixtureRun = Guid.CreateVersion7();
        var tenantId = Guid.Empty;
        var legalEntityId = Guid.Empty;
        var leaveTypeId = Guid.Empty;
        var actorId = Guid.Empty;
        const int expiredCount = 10_000;
        const int futureCount = 10_000;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select tenant.id,entity.id,leave_type.id,identity.id from platform.tenant tenant join platform.legal_entity entity on entity.tenant_id=tenant.id and entity.status='Active' join business_hr.leave_type leave_type on leave_type.tenant_id=tenant.id and leave_type.legal_entity_id=entity.id and leave_type.code='COMPANY-ANNUAL' join platform.identity identity on identity.tenant_id=tenant.id and identity.normalized_email='ADMIN@ATLAS.COM' where tenant.slug='atlas-trading' order by entity.id limit 1", admin, transaction))
            await using (var reader = await scope.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow))
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The local performance database must contain the canonical Atlas HR seed.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
                leaveTypeId = reader.GetGuid(2);
                actorId = reader.GetGuid(3);
            }

            await using (var insert = new NpgsqlCommand("""
                with eligible as (
                    select employment.id,row_number() over(order by employment.employment_number) as sequence
                    from business_hr.employment employment
                    where employment.tenant_id=$1 and employment.legal_entity_id=$2
                      and employment.employment_number like 'PERF-%'
                      and employment.status='Active' and employment.end_date is null
                      and employment.start_date<=date '2024-01-01'
                    order by employment.employment_number
                    limit 20000
                )
                insert into business_hr.leave_balance_movement(
                    id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,
                    source_type,source_reference,idempotency_key,policy_version,expires_on,reason,created_by,created_at)
                select md5($3::text || ':' || eligible.sequence::text)::uuid,$1,$2,eligible.id,$4,date '2024-01-01','CarryOver',
                       case when eligible.sequence<=$5 then 5 else 7 end,
                       'HrScaleExpiryFixture',$3::text || ':' || eligible.sequence::text,
                       'hr-scale-expiry:' || $3::text || ':' || eligible.sequence::text,
                       1,case when eligible.sequence<=$5 then $6::date else $7::date end,
                       'Representative local HR entitlement expiry performance fixture',$8,now()
                from eligible
                """, admin, transaction))
            {
                insert.Parameters.AddWithValue(tenantId);
                insert.Parameters.AddWithValue(legalEntityId);
                insert.Parameters.AddWithValue(fixtureRun);
                insert.Parameters.AddWithValue(leaveTypeId);
                insert.Parameters.AddWithValue(expiredCount);
                insert.Parameters.AddWithValue(asOfDate);
                insert.Parameters.AddWithValue(asOfDate.AddYears(1));
                insert.Parameters.AddWithValue(actorId);
                Assert.Equal(expiredCount + futureCount, await insert.ExecuteNonQueryAsync());
            }
            await transaction.CommitAsync();
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new LeaveRequestRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource, TimeProvider.System);
        var tenant = new TenantId(tenantId);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var batchCounts = new List<int>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var inserted = await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, asOfDate, CancellationToken.None);
            Assert.InRange(inserted, 0, 5000);
            batchCounts.Add(inserted);
            if (inserted == 0) break;
        }
        stopwatch.Stop();

        Assert.Contains(batchCounts, inserted => inserted == 5000);
        Assert.Contains(batchCounts, inserted => inserted > 0);
        Assert.Equal(0, batchCounts[^1]);

        await using var verify = new NpgsqlConnection(adminConnectionString);
        await verify.OpenAsync();
        await using (var reconcile = new NpgsqlCommand("""
            select count(*) filter(where carry.expires_on<=$4),
                   count(*) filter(where carry.expires_on>$4),
                   count(*) filter(where carry.expires_on<=$4 and expiry.id is not null),
                   count(*) filter(where carry.expires_on>$4 and expiry.id is not null),
                   coalesce(sum(expiry.quantity) filter(where carry.expires_on<=$4),0)
            from business_hr.leave_balance_movement carry
            left join business_hr.leave_balance_allocation allocation on allocation.tenant_id=carry.tenant_id and allocation.entitlement_movement_id=carry.id
            left join business_hr.leave_balance_movement expiry on expiry.tenant_id=allocation.tenant_id and expiry.id=allocation.movement_id
                and expiry.movement_type='Expiry' and expiry.source_type='LeaveEntitlementExpiry'
            where carry.tenant_id=$1 and carry.leave_type_id=$2 and carry.source_type='HrScaleExpiryFixture' and carry.source_reference like $3 || ':%'
            """, verify))
        {
            reconcile.Parameters.AddWithValue(tenantId);
            reconcile.Parameters.AddWithValue(leaveTypeId);
            reconcile.Parameters.AddWithValue(fixtureRun.ToString("D"));
            reconcile.Parameters.AddWithValue(asOfDate);
            await using var reader = await reconcile.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
            Assert.True(await reader.ReadAsync());
            Assert.Equal(expiredCount, reader.GetInt64(0));
            Assert.Equal(futureCount, reader.GetInt64(1));
            Assert.Equal(expiredCount, reader.GetInt64(2));
            Assert.Equal(0, reader.GetInt64(3));
            Assert.Equal(-expiredCount * 5m, reader.GetDecimal(4));
        }

        var repeated = await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, asOfDate, CancellationToken.None);
        Assert.Equal(0, repeated);
        output.WriteLine($"Carryover expiry fixture: expired={expiredCount}, future={futureCount}, batches={string.Join(',', batchCounts)}, elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F1}, repeated={repeated}.");
    }

    private sealed class HrScaleExpiryFactAttribute : FactAttribute
    {
        public HrScaleExpiryFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_SCALE_EXPIRY_TEST"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_SCALE_EXPIRY_TEST=1 only for the dedicated local todir_hr_perf_ PostgreSQL database.";
        }
    }
}
