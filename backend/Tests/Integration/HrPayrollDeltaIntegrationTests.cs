using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Repositories;
using Npgsql;
using Shared.Contracts.Hr;
using Shared.Primitives.Identifiers;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrPayrollDeltaIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task PayrollLockedAttendanceCorrectionCreatesImmutableDeltaWithoutChangingConsumedRecord()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var tenantId = Guid.Empty;
        var legalEntityId = Guid.Empty;
        var employeeId = Guid.CreateVersion7();
        var employmentId = Guid.CreateVersion7();
        var attendanceId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var workDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-Random.Shared.Next(1, 3000));
        var employmentNumber = $"HRIT-{employmentId:N}";
        var payrollRunId = Guid.CreateVersion7();
        var payrollReference = $"HRIT-{payrollRunId:N}";

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using (var scope = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id where t.slug='atlas-trading' and e.status='Active' order by e.id limit 1", admin))
            await using (var reader = await scope.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
            }

            await using (var insertEmployee = new NpgsqlCommand("insert into business_hr.employee(tenant_id,id,arabic_name,english_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,created_by,created_at,version) values($1,$2,$3,$4,$4,$4,$3,$4,'Active',$5,$6,1)", admin))
            {
                insertEmployee.Parameters.AddWithValue(tenantId); insertEmployee.Parameters.AddWithValue(employeeId);
                insertEmployee.Parameters.AddWithValue("اختبار تكامل الموارد البشرية"); insertEmployee.Parameters.AddWithValue($"HR Integration {employmentId:N}");
                insertEmployee.Parameters.AddWithValue(actorId); insertEmployee.Parameters.AddWithValue(now);
                await insertEmployee.ExecuteNonQueryAsync();
            }
            await using (var insertEmployment = new NpgsqlCommand("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,created_by,created_at,version) values($1,$2,$3,$4,$5,'FullTime','Active',$6,$7,$8,1)", admin))
            {
                insertEmployment.Parameters.AddWithValue(tenantId); insertEmployment.Parameters.AddWithValue(employmentId); insertEmployment.Parameters.AddWithValue(employeeId);
                insertEmployment.Parameters.AddWithValue(legalEntityId); insertEmployment.Parameters.AddWithValue(employmentNumber); insertEmployment.Parameters.AddWithValue(workDate.AddDays(-30));
                insertEmployment.Parameters.AddWithValue(actorId); insertEmployment.Parameters.AddWithValue(now);
                await insertEmployment.ExecuteNonQueryAsync();
            }
            await using (var insertAttendance = new NpgsqlCommand("insert into business_hr.attendance_record(id,tenant_id,employee_id,employment_id,legal_entity_id,work_date,time_zone_id,planned_start_local,planned_end_local,actual_first_in,actual_last_out,worked_minutes,attendance_status,normalization_version,lock_state,version,updated_at) values($1,$2,$3,$4,$5,$6,'UTC','09:00','17:00',$7,$8,420,'Present',1,'Open',1,$9)", admin))
            {
                insertAttendance.Parameters.AddWithValue(attendanceId); insertAttendance.Parameters.AddWithValue(tenantId); insertAttendance.Parameters.AddWithValue(employeeId);
                insertAttendance.Parameters.AddWithValue(employmentId); insertAttendance.Parameters.AddWithValue(legalEntityId); insertAttendance.Parameters.AddWithValue(workDate);
                insertAttendance.Parameters.AddWithValue(new DateTimeOffset(workDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc)));
                insertAttendance.Parameters.AddWithValue(new DateTimeOffset(workDate.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc)));
                insertAttendance.Parameters.AddWithValue(now);
                await insertAttendance.ExecuteNonQueryAsync();
            }
        }

        var tenant = new TenantId(tenantId);
        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var coordinator = new NpgsqlTransactionCoordinator(dataSource);
        var payroll = new PayrollIntegrationRepository(coordinator, TimeProvider.System);
        var attendance = new AttendanceRepository(coordinator, dataSource, TimeProvider.System);
        var cutoff = DateTimeOffset.UtcNow;
        var snapshot = await payroll.CreateSnapshotAsync(tenant, new(legalEntityId, workDate, workDate, cutoff), CancellationToken.None);
        Assert.Contains(snapshot.Employments, item => item.EmploymentId == employmentId);

        var locked = await payroll.LockAttendancePeriodAsync(tenant,
            new(legalEntityId, payrollRunId, payrollReference, workDate, workDate, snapshot.SnapshotId), CancellationToken.None);
        Assert.Equal(1, locked.LockedRecordCount);

        long lockedVersion;
        DateTimeOffset firstIn;
        DateTimeOffset lastOut;
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction))
            {
                setTenant.Parameters.AddWithValue(tenantId.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();
            }
            await using var read = new NpgsqlCommand("select version,actual_first_in,actual_last_out from business_hr.attendance_record where tenant_id=$1 and id=$2 and lock_state='LockedForPayroll'", connection, transaction);
            read.Parameters.AddWithValue(tenantId); read.Parameters.AddWithValue(attendanceId);
            await using var reader = await read.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            lockedVersion = reader.GetInt64(0);
            firstIn = reader.GetFieldValue<DateTimeOffset>(1);
            lastOut = reader.GetFieldValue<DateTimeOffset>(2);
            await reader.CloseAsync();
            await transaction.CommitAsync();
        }

        var correction = await attendance.CorrectAsync(tenant, actorId, Guid.CreateVersion7(), attendanceId, legalEntityId,
            new(lockedVersion, firstIn.AddMinutes(2), lastOut, "SOURCE_CORRECTION", "Source attendance evidence was received after the payroll cutoff."),
            Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal("PayrollDeltaPending", correction.Status);

        var deltas = await payroll.ListAttendanceCorrectionDeltasAsync(tenant, snapshot.SnapshotId, CancellationToken.None);
        var delta = Assert.Single(deltas);
        Assert.Equal(correction.Id, delta.CorrectionId);
        Assert.Equal(snapshot.SnapshotId, delta.SnapshotId);
        Assert.Equal("SOURCE_CORRECTION", delta.ReasonCode);
        Assert.NotEqual(delta.Before.GetRawText(), delta.Proposed.GetRawText());

        await using var verifyConnection = await dataSource.OpenConnectionAsync();
        await using var verifyTransaction = await verifyConnection.BeginTransactionAsync();
        await using (var setTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", verifyConnection, verifyTransaction))
        {
            setTenant.Parameters.AddWithValue(tenantId.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();
        }
        await using var verify = new NpgsqlCommand("select version,actual_first_in,lock_state,payroll_reference from business_hr.attendance_record where tenant_id=$1 and id=$2", verifyConnection, verifyTransaction);
        verify.Parameters.AddWithValue(tenantId); verify.Parameters.AddWithValue(attendanceId);
        await using var verifyReader = await verify.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        Assert.Equal(lockedVersion, verifyReader.GetInt64(0));
        Assert.Equal(firstIn, verifyReader.GetFieldValue<DateTimeOffset>(1));
        Assert.Equal("LockedForPayroll", verifyReader.GetString(2));
        Assert.Equal(payrollReference, verifyReader.GetString(3));
        await verifyReader.CloseAsync();
        await verifyTransaction.CommitAsync();
    }

    private sealed class HrPostgresFactAttribute : FactAttribute
    {
        public HrPostgresFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_TEST_DATABASE"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_TEST_DATABASE=1 only for a disposable PostgreSQL database with the canonical development seed.";
        }
    }
}
