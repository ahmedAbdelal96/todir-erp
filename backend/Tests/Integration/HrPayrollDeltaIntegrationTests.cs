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
    public async Task PayrollSnapshotPreservesCompensationChangeWithinThePeriod()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var employeeId = Guid.CreateVersion7();
        var employmentId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var leaveTypeId = Guid.CreateVersion7();
        var leavePolicyId = Guid.CreateVersion7();
        var leaveRequestId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateOnly(now.Year, now.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var changeDate = periodStart.AddDays(Math.Min(14, periodEnd.Day - 1));
        Guid tenantId;
        Guid legalEntityId;

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
                insertEmployee.Parameters.AddWithValue("اختبار تغير الراتب"); insertEmployee.Parameters.AddWithValue($"HR Salary Change {employmentId:N}");
                insertEmployee.Parameters.AddWithValue(actorId); insertEmployee.Parameters.AddWithValue(now);
                await insertEmployee.ExecuteNonQueryAsync();
            }
            await using (var insertEmployment = new NpgsqlCommand("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,created_by,created_at,version) values($1,$2,$3,$4,$5,'FullTime','Active',$6,$7,$8,1)", admin))
            {
                insertEmployment.Parameters.AddWithValue(tenantId); insertEmployment.Parameters.AddWithValue(employmentId); insertEmployment.Parameters.AddWithValue(employeeId);
                insertEmployment.Parameters.AddWithValue(legalEntityId); insertEmployment.Parameters.AddWithValue($"HRIT-{employmentId:N}"); insertEmployment.Parameters.AddWithValue(periodStart.AddMonths(-1));
                insertEmployment.Parameters.AddWithValue(actorId); insertEmployment.Parameters.AddWithValue(now);
                await insertEmployment.ExecuteNonQueryAsync();
            }
            await using (var insertCompensation = new NpgsqlCommand("insert into business_hr.employment_compensation_agreement(id,tenant_id,employment_id,legal_entity_id,currency_code,amount,pay_frequency,effective_from,effective_to,status,reason,created_by,created_at,version) values($1,$2,$3,$4,'EGP',30000,'Monthly',$5,$6,'Superseded','Monthly salary before approved change',$7,$8,1),($9,$2,$3,$4,'EGP',36000,'Monthly',$10,null,'Active','Approved mid-month salary change',$7,$8,1)", admin))
            {
                insertCompensation.Parameters.AddWithValue(Guid.CreateVersion7()); insertCompensation.Parameters.AddWithValue(tenantId); insertCompensation.Parameters.AddWithValue(employmentId);
                insertCompensation.Parameters.AddWithValue(legalEntityId); insertCompensation.Parameters.AddWithValue(periodStart); insertCompensation.Parameters.AddWithValue(changeDate.AddDays(-1));
                insertCompensation.Parameters.AddWithValue(actorId); insertCompensation.Parameters.AddWithValue(now); insertCompensation.Parameters.AddWithValue(Guid.CreateVersion7());
                insertCompensation.Parameters.AddWithValue(changeDate);
                await insertCompensation.ExecuteNonQueryAsync();
            }
            await using (var insertLeaveType = new NpgsqlCommand("insert into business_hr.leave_type(id,tenant_id,legal_entity_id,code,name_ar,name_en,category,unit,status,created_by,created_at) values($1,$2,$3,$4,'إجازة اختبار','Integration Leave','Annual','Days','Active',$5,$6)", admin))
            {
                insertLeaveType.Parameters.AddWithValue(leaveTypeId); insertLeaveType.Parameters.AddWithValue(tenantId); insertLeaveType.Parameters.AddWithValue(legalEntityId);
                insertLeaveType.Parameters.AddWithValue($"IT-{leaveTypeId:N}"); insertLeaveType.Parameters.AddWithValue(actorId); insertLeaveType.Parameters.AddWithValue(now);
                await insertLeaveType.ExecuteNonQueryAsync();
            }
            await using (var insertLeavePolicy = new NpgsqlCommand("insert into business_hr.leave_policy(id,tenant_id,legal_entity_id,leave_type_id,version,effective_from,counting_method,approval_required,partial_day_allowed,negative_balance_policy,negative_balance_limit,payroll_treatment,accrual_rule,carryover_rule,eligibility_rules,created_by,created_at) values($1,$2,$3,$4,1,$5,'WorkingDays',true,false,'Disallow',0,'Paid','{\"mode\":\"None\"}'::jsonb,'{}'::jsonb,'{}'::jsonb,$6,$7)", admin))
            {
                insertLeavePolicy.Parameters.AddWithValue(leavePolicyId); insertLeavePolicy.Parameters.AddWithValue(tenantId); insertLeavePolicy.Parameters.AddWithValue(legalEntityId);
                insertLeavePolicy.Parameters.AddWithValue(leaveTypeId); insertLeavePolicy.Parameters.AddWithValue(periodStart); insertLeavePolicy.Parameters.AddWithValue(actorId);
                insertLeavePolicy.Parameters.AddWithValue(now); await insertLeavePolicy.ExecuteNonQueryAsync();
            }
            await using (var insertLeaveRequest = new NpgsqlCommand("insert into business_hr.leave_request(id,tenant_id,legal_entity_id,employee_id,employment_id,leave_type_id,policy_id,requested_from,requested_to,charged_quantity,reason,status,version,idempotency_key,submitted_at,decided_by,decided_at,decision_reason,created_by,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$8,1,'Test leave status at payroll cutoff','Cancelled',2,$9,$10,$11,$12,'Cancelled after payroll cutoff',$11,$10,$12)", admin))
            {
                insertLeaveRequest.Parameters.AddWithValue(leaveRequestId); insertLeaveRequest.Parameters.AddWithValue(tenantId); insertLeaveRequest.Parameters.AddWithValue(legalEntityId);
                insertLeaveRequest.Parameters.AddWithValue(employeeId); insertLeaveRequest.Parameters.AddWithValue(employmentId); insertLeaveRequest.Parameters.AddWithValue(leaveTypeId);
                insertLeaveRequest.Parameters.AddWithValue(leavePolicyId); insertLeaveRequest.Parameters.AddWithValue(periodStart); insertLeaveRequest.Parameters.AddWithValue($"hr-it-{leaveRequestId:N}");
                insertLeaveRequest.Parameters.AddWithValue(now.AddMinutes(-2)); insertLeaveRequest.Parameters.AddWithValue(actorId); insertLeaveRequest.Parameters.AddWithValue(now.AddMinutes(2));
                await insertLeaveRequest.ExecuteNonQueryAsync();
            }
            await using (var insertLeaveEvents = new NpgsqlCommand("insert into business_hr.leave_request_event(id,tenant_id,legal_entity_id,leave_request_id,event_type,from_status,to_status,actor_id,reason,occurred_at) values($1,$2,$3,$4,'Approved','Pending','Approved',$5,'Approved before cutoff',$6),($7,$2,$3,$4,'Cancelled','Approved','Cancelled',$5,'Cancelled after cutoff',$8)", admin))
            {
                insertLeaveEvents.Parameters.AddWithValue(Guid.CreateVersion7()); insertLeaveEvents.Parameters.AddWithValue(tenantId); insertLeaveEvents.Parameters.AddWithValue(legalEntityId);
                insertLeaveEvents.Parameters.AddWithValue(leaveRequestId); insertLeaveEvents.Parameters.AddWithValue(actorId); insertLeaveEvents.Parameters.AddWithValue(now.AddMinutes(-1));
                insertLeaveEvents.Parameters.AddWithValue(Guid.CreateVersion7()); insertLeaveEvents.Parameters.AddWithValue(now.AddMinutes(1));
                await insertLeaveEvents.ExecuteNonQueryAsync();
            }
            await using (var insertLeaveCharge = new NpgsqlCommand("insert into business_hr.leave_request_charge(id,tenant_id,legal_entity_id,leave_request_id,employment_id,leave_type_id,charge_date,quantity,unit,policy_version) values($1,$2,$3,$4,$5,$6,$7,1,'Days',1)", admin))
            {
                insertLeaveCharge.Parameters.AddWithValue(Guid.CreateVersion7()); insertLeaveCharge.Parameters.AddWithValue(tenantId); insertLeaveCharge.Parameters.AddWithValue(legalEntityId);
                insertLeaveCharge.Parameters.AddWithValue(leaveRequestId); insertLeaveCharge.Parameters.AddWithValue(employmentId); insertLeaveCharge.Parameters.AddWithValue(leaveTypeId);
                insertLeaveCharge.Parameters.AddWithValue(periodStart); await insertLeaveCharge.ExecuteNonQueryAsync();
            }
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var payroll = new PayrollIntegrationRepository(new NpgsqlTransactionCoordinator(dataSource), TimeProvider.System);
        var snapshot = await payroll.CreateSnapshotAsync(new TenantId(tenantId),
            new(legalEntityId, periodStart, periodEnd, now), CancellationToken.None);
        var employment = Assert.Single(snapshot.Employments, item => item.EmploymentId == employmentId);
        Assert.Collection(employment.Compensation,
            before => { Assert.Equal(30000m, before.Amount); Assert.Equal(periodStart, before.EffectiveFrom); Assert.Equal(changeDate.AddDays(-1), before.EffectiveTo); },
            after => { Assert.Equal(36000m, after.Amount); Assert.Equal(changeDate, after.EffectiveFrom); Assert.Null(after.EffectiveTo); });
        var leave = Assert.Single(employment.Leave);
        Assert.Equal(leaveRequestId, leave.LeaveRequestId);
        Assert.Equal("Approved", leave.StatusAtCutoff);
        Assert.Equal("Paid", leave.PayrollTreatment);
        Assert.Equal(1m, leave.ChargedQuantityInPeriod);
    }

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
