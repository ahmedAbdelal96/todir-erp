-- Run once in a freshly migrated and seeded local performance database.
-- This generator adds 99,920 active employees (bringing the tenant above 100k),
-- 999,200 timeline events, a 1,000,000-movement leave ledger, and 10,000,000
-- attendance rows. All identifiers and distributions are deterministic.
DO $$
DECLARE
    target_tenant uuid;
    existing_employees integer;
BEGIN
    SELECT id INTO target_tenant FROM platform.tenant WHERE slug='atlas-trading';
    IF target_tenant IS NULL THEN RAISE EXCEPTION 'canonical Atlas seed is required'; END IF;
    SELECT count(*) INTO existing_employees FROM business_hr.employee WHERE tenant_id=target_tenant;
    IF existing_employees <> 81 THEN RAISE EXCEPTION 'expected 81 base employees in a fresh seed, found %', existing_employees; END IF;
END $$;

CREATE TEMP TABLE hr_perf_context ON COMMIT PRESERVE ROWS AS
SELECT tenant.id AS tenant_id,
       entity.id AS legal_entity_id,
       branch.id AS branch_id,
       department.id AS department_id,
       position.id AS position_id,
       calendar.id AS calendar_id,
       leave_type.id AS leave_type_id,
       identity.id AS actor_id
FROM platform.tenant tenant
JOIN platform.legal_entity entity ON entity.tenant_id=tenant.id AND entity.status='Active'
JOIN platform.branch branch ON branch.tenant_id=tenant.id AND branch.legal_entity_id=entity.id
JOIN business_hr.department department ON department.tenant_id=tenant.id AND department.legal_entity_id=entity.id
JOIN business_hr.position position ON position.tenant_id=tenant.id AND position.legal_entity_id=entity.id
JOIN business_hr.work_calendar calendar ON calendar.tenant_id=tenant.id AND calendar.legal_entity_id=entity.id
JOIN business_hr.leave_type leave_type ON leave_type.tenant_id=tenant.id AND leave_type.legal_entity_id=entity.id AND leave_type.code='COMPANY-ANNUAL'
JOIN platform.identity identity ON identity.tenant_id=tenant.id AND identity.normalized_email='ADMIN@ATLAS.COM'
WHERE tenant.slug='atlas-trading'
ORDER BY department.code, position.code, branch.id, calendar.code
LIMIT 1;

INSERT INTO business_hr.employee(tenant_id,id,arabic_name,english_name,preferred_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,membership_id,created_by,created_at,version)
SELECT context.tenant_id,
       md5('hr-scale-employee:' || context.tenant_id::text || ':' || series.n)::uuid,
       (ARRAY['أحمد','مريم','عمر','نور','سلمى','يوسف','ندى','كريم'])[1 + series.n % 8] || ' ' || (ARRAY['حسن','علي','منصور','إبراهيم','السيد','محمود'])[1 + series.n % 6],
       (ARRAY['Ahmed','Mariam','Omar','Nour','Salma','Youssef','Nada','Karim'])[1 + series.n % 8] || ' ' || (ARRAY['Hassan','Ali','Mansour','Ibrahim','Elsayed','Mahmoud'])[1 + series.n % 6],
       NULL,
       'موظف تجريبي ' || lpad(series.n::text,6,'0'),
       'موظف تجريبي ' || lpad(series.n::text,6,'0'),
       'موظف تجريبي ' || lpad(series.n::text,6,'0'),
       'hr perf ' || lpad(series.n::text,6,'0'),
       'Active',NULL,context.actor_id,now(),1
FROM hr_perf_context context CROSS JOIN generate_series(1,99920) AS series(n);

INSERT INTO business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at)
SELECT employee.tenant_id,employee.id,employee.display_name,employee.display_name_normalized,employee.arabic_name_normalized,employee.english_name,employee.english_name_normalized,employee.status,now()
FROM business_hr.employee employee JOIN hr_perf_context context USING (tenant_id)
WHERE employee.display_name LIKE 'موظف تجريبي %';

INSERT INTO business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version)
SELECT context.tenant_id,
       md5('hr-scale-employment:' || context.tenant_id::text || ':' || series.n)::uuid,
       md5('hr-scale-employee:' || context.tenant_id::text || ':' || series.n)::uuid,
       context.legal_entity_id,
       'PERF-' || lpad(series.n::text,6,'0'),
       CASE WHEN series.n % 20 = 0 THEN 'PartTime' ELSE 'FullTime' END,
       'Active',
       date '2018-01-01' + (series.n % 2900),NULL,context.actor_id,now(),1
FROM hr_perf_context context CROSS JOIN generate_series(1,99920) AS series(n);

SELECT format($sql$
    INSERT INTO business_hr.employment_assignment(tenant_id,id,legal_entity_id,employment_id,branch_id,department_id,position_id,manager_employment_id,work_calendar_id,effective_from,effective_to,reason_code,created_by,created_at,version)
    SELECT context.tenant_id,
           md5('hr-scale-assignment:' || context.tenant_id::text || ':' || series.n)::uuid,
           context.legal_entity_id,
           md5('hr-scale-employment:' || context.tenant_id::text || ':' || series.n)::uuid,
           context.branch_id,context.department_id,context.position_id,NULL,context.calendar_id,
           date '2018-01-01' + mod(series.n,2900),NULL,'InitialHire',context.actor_id,now(),1
    FROM hr_perf_context context CROSS JOIN generate_series(%s,%s) AS series(n)
    $sql$,first_n,least(first_n+1999,99920))
FROM generate_series(1,99920,2000) AS batches(first_n)
\gexec

INSERT INTO business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at)
SELECT context.tenant_id,
       md5('hr-scale-event:' || context.tenant_id::text || ':' || series.n || ':' || event_no)::uuid,
       md5('hr-scale-employee:' || context.tenant_id::text || ':' || series.n)::uuid,
       md5('hr-scale-employment:' || context.tenant_id::text || ':' || series.n)::uuid,
       'DevelopmentTimelineEvent',date '2019-01-01' + ((series.n % 2500) + (event_no * 90)),context.actor_id,
       jsonb_build_object('eventIndex',event_no,'source','local-performance-fixture'),
       md5('hr-scale-correlation:' || context.tenant_id::text || ':' || series.n || ':' || event_no)::uuid,
       timestamptz '2020-01-01 00:00:00+00' + (event_no || ' months')::interval
FROM hr_perf_context context
CROSS JOIN generate_series(1,99920) AS series(n)
CROSS JOIN generate_series(1,10) AS events(event_no);

INSERT INTO business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload)
SELECT event.tenant_id,event.id,event.employee_id,event.employment_id,event.event_type,event.effective_on,event.occurred_at,event.payload
FROM business_hr.employment_event event JOIN hr_perf_context context USING (tenant_id)
WHERE event.event_type='DevelopmentTimelineEvent';

INSERT INTO business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,expires_on,reason,created_by,created_at)
SELECT md5('hr-scale-leave:' || context.tenant_id::text || ':' || series.n || ':' || movement_no)::uuid,
       context.tenant_id,context.legal_entity_id,
       md5('hr-scale-employment:' || context.tenant_id::text || ':' || series.n)::uuid,
       context.leave_type_id,
       date '2021-01-01' + (movement_no * 90),
       CASE WHEN movement_no % 4=0 THEN 'Adjustment' ELSE 'Accrual' END,
       CASE WHEN movement_no % 4=0 THEN -0.5 ELSE 1.75 END,
       'DevelopmentPerformance',
       'annual-movement-' || movement_no,
       'hr-scale-leave:' || context.tenant_id::text || ':' || series.n || ':' || movement_no,
       1,NULL,'Deterministic local performance history.',context.actor_id,now()
FROM hr_perf_context context
CROSS JOIN generate_series(1,99920) AS series(n)
CROSS JOIN generate_series(1,10) AS movements(movement_no);

INSERT INTO business_hr.employment_contract(id,tenant_id,legal_entity_id,employment_id,contract_type,start_date,end_date,status,created_by,created_at,version)
SELECT md5('hr-scale-expiring-contract:' || context.tenant_id::text || ':' || series.n)::uuid,
       context.tenant_id,context.legal_entity_id,
       md5('hr-scale-employment:' || context.tenant_id::text || ':' || series.n)::uuid,
       'FixedTerm',(current_date - interval '2 years')::date + (series.n % 180),
       current_date + (series.n % 120),'Active',context.actor_id,now(),1
FROM hr_perf_context context CROSS JOIN generate_series(1,10000) AS series(n);

INSERT INTO business_hr.attendance_record(id,tenant_id,employee_id,employment_id,legal_entity_id,work_date,work_calendar_id,shift_assignment_id,time_zone_id,planned_start_local,planned_end_local,planned_break_minutes,grace_minutes,actual_first_in,actual_last_out,worked_minutes,late_minutes,early_leave_minutes,overtime_eligible_minutes,attendance_status,normalization_version,lock_state,payroll_reference,version,updated_at)
SELECT md5('hr-scale-attendance:' || employment.tenant_id::text || ':' || employment.id::text || ':' || date_value.work_date)::uuid,
       employment.tenant_id,employment.employee_id,employment.id,employment.legal_entity_id,
       date_value.work_date,context.calendar_id,NULL,'Africa/Cairo','09:00','17:00',60,10,
       (date_value.work_date::timestamp + time '09:00') AT TIME ZONE 'Africa/Cairo',
       (date_value.work_date::timestamp + time '17:00') AT TIME ZONE 'Africa/Cairo',
       CASE WHEN day_offset.sequence % 100 IN (97,98,99) THEN 420 ELSE 480 END,
       CASE WHEN day_offset.sequence % 7=0 THEN 12 ELSE 0 END,
       0,CASE WHEN day_offset.sequence % 17=0 THEN 60 ELSE 0 END,
       CASE WHEN day_offset.sequence % 100=97 THEN 'Partial'
            WHEN day_offset.sequence % 100=98 THEN 'Absent'
            WHEN day_offset.sequence % 100=99 THEN 'Exception'
            ELSE 'Present' END,
       1,'Open',NULL,1,now()
FROM business_hr.employment employment
JOIN hr_perf_context context ON context.tenant_id=employment.tenant_id
CROSS JOIN LATERAL generate_series(0,99) AS day_offset(sequence)
CROSS JOIN LATERAL (SELECT date '2024-01-01' + day_offset.sequence AS work_date) date_value;

ANALYZE business_hr.employee;
ANALYZE business_hr.employee_list_projection;
ANALYZE business_hr.employment;
ANALYZE business_hr.employment_event;
ANALYZE business_hr.employee_timeline_projection;
ANALYZE business_hr.leave_balance_movement;
ANALYZE business_hr.employment_contract;
ANALYZE business_hr.attendance_record;

SELECT 'employees' AS dataset,count(*) FROM business_hr.employee employee JOIN hr_perf_context context USING (tenant_id)
UNION ALL SELECT 'active_employments',count(*) FROM business_hr.employment employment JOIN hr_perf_context context USING (tenant_id) WHERE employment.status='Active'
UNION ALL SELECT 'attendance',count(*) FROM business_hr.attendance_record attendance JOIN hr_perf_context context USING (tenant_id)
UNION ALL SELECT 'leave_movements',count(*) FROM business_hr.leave_balance_movement movement JOIN hr_perf_context context USING (tenant_id)
UNION ALL SELECT 'timeline_events',count(*) FROM business_hr.employment_event event JOIN hr_perf_context context USING (tenant_id) WHERE event.event_type='DevelopmentTimelineEvent'
UNION ALL SELECT 'expiring_contracts',count(*) FROM business_hr.employment_contract contract JOIN hr_perf_context context USING (tenant_id) WHERE contract.status='Active' AND contract.end_date BETWEEN current_date AND current_date + 120;
