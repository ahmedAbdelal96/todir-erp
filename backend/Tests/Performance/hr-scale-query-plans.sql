\timing on
SELECT id AS tenant_id FROM platform.tenant WHERE slug='atlas-trading' \gset
SELECT id AS legal_entity_id FROM platform.legal_entity WHERE tenant_id=:'tenant_id'::uuid AND status='Active' ORDER BY id LIMIT 1 \gset
SET ROLE erp_app;
SELECT set_config('app.tenant_id', :'tenant_id', false);

\echo 'Employee search and first page'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
WITH candidate_ids AS (
  SELECT employee.employee_id FROM business_hr.employee_list_projection employee
  WHERE employee.tenant_id=:'tenant_id'::uuid AND employee.status<>'Archived'
    AND (employee.arabic_name_normalized LIKE '%perf-050000%' OR employee.english_name_normalized LIKE '%perf-050000%')
  UNION
  SELECT searched.employee_id FROM business_hr.employment searched
  WHERE searched.tenant_id=:'tenant_id'::uuid AND searched.legal_entity_id=:'legal_entity_id'::uuid
    AND lower(searched.employment_number) LIKE '%perf-050000%'
)
SELECT count(*) FROM business_hr.employee_list_projection employee
WHERE employee.tenant_id=:'tenant_id'::uuid AND employee.status<>'Archived'
  AND employee.employee_id IN (SELECT employee_id FROM candidate_ids)
  AND EXISTS (SELECT 1 FROM business_hr.employment scoped WHERE scoped.tenant_id=employee.tenant_id
    AND scoped.employee_id=employee.employee_id AND scoped.legal_entity_id=:'legal_entity_id'::uuid
    AND scoped.status IN ('PreHire','Active','Suspended','NoticePeriod'));

EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
WITH candidate_ids AS (
  SELECT employee.employee_id FROM business_hr.employee_list_projection employee
  WHERE employee.tenant_id=:'tenant_id'::uuid AND employee.status<>'Archived'
    AND (employee.arabic_name_normalized LIKE '%perf-050000%' OR employee.english_name_normalized LIKE '%perf-050000%')
  UNION
  SELECT searched.employee_id FROM business_hr.employment searched
  WHERE searched.tenant_id=:'tenant_id'::uuid AND searched.legal_entity_id=:'legal_entity_id'::uuid
    AND lower(searched.employment_number) LIKE '%perf-050000%'
)
SELECT employee.employee_id,employment.employment_number,employee.display_name,employee.english_name,employee.status,employment.start_date,employment.legal_entity_id
FROM business_hr.employee_list_projection employee
LEFT JOIN LATERAL (
  SELECT employment_number,start_date,legal_entity_id FROM business_hr.employment employment
  WHERE employment.tenant_id=employee.tenant_id AND employment.employee_id=employee.employee_id
    AND employment.status IN ('PreHire','Active','Suspended','NoticePeriod')
    AND employment.legal_entity_id=:'legal_entity_id'::uuid
  ORDER BY start_date DESC,id LIMIT 1
) employment ON true
WHERE employee.tenant_id=:'tenant_id'::uuid AND employee.status<>'Archived'
  AND employee.employee_id IN (SELECT employee_id FROM candidate_ids)
  AND EXISTS (SELECT 1 FROM business_hr.employment scoped WHERE scoped.tenant_id=employee.tenant_id
    AND scoped.employee_id=employee.employee_id AND scoped.legal_entity_id=:'legal_entity_id'::uuid
    AND scoped.status IN ('PreHire','Active','Suspended','NoticePeriod'))
ORDER BY employee.display_name_normalized,employee.employee_id
LIMIT 50 OFFSET 0;

\echo 'Legal-entity attendance daily list'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT id,employee_id,employment_id,attendance_status,worked_minutes,late_minutes
FROM business_hr.attendance_record
WHERE tenant_id=:'tenant_id'::uuid AND legal_entity_id=:'legal_entity_id'::uuid
  AND work_date=date '2024-02-10'
ORDER BY work_date DESC,employee_id,id
LIMIT 100;

\echo 'Employee attendance history page'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT work_date,attendance_status,worked_minutes
FROM business_hr.attendance_record
WHERE tenant_id=:'tenant_id'::uuid
  AND employment_id=md5('hr-scale-employment:' || :'tenant_id' || ':50000')::uuid
ORDER BY work_date DESC,id DESC
LIMIT 50;

\echo 'Leave movement history page'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT effective_date,movement_type,quantity
FROM business_hr.leave_balance_movement
WHERE tenant_id=:'tenant_id'::uuid
  AND employment_id=md5('hr-scale-employment:' || :'tenant_id' || ':50000')::uuid
  AND leave_type_id=(SELECT id FROM business_hr.leave_type WHERE tenant_id=:'tenant_id'::uuid AND code='COMPANY-ANNUAL')
ORDER BY effective_date,id
LIMIT 50;

\echo 'Contract expiry window scan'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT contract.id,contract.employment_id,contract.end_date
FROM business_hr.employment_contract contract
WHERE contract.tenant_id=:'tenant_id'::uuid AND contract.legal_entity_id=:'legal_entity_id'::uuid
  AND contract.status IN ('Active','Expired')
  AND contract.end_date BETWEEN current_date-30 AND current_date+90
ORDER BY contract.end_date,contract.id
LIMIT 100;

\echo 'HR Home attendance counts by date'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT count(*) AS all_rows,
       count(*) FILTER (WHERE attendance_status='Present') AS present_rows,
       count(*) FILTER (WHERE attendance_status='Absent') AS absent_rows
FROM business_hr.attendance_record
WHERE tenant_id=:'tenant_id'::uuid AND legal_entity_id=:'legal_entity_id'::uuid
  AND work_date=date '2024-02-10';

\echo 'Multi-year timeline cursor page'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT effective_on,event_type,occurred_at
FROM business_hr.employee_timeline_projection
WHERE tenant_id=:'tenant_id'::uuid
  AND employee_id=md5('hr-scale-employee:' || :'tenant_id' || ':50000')::uuid
ORDER BY effective_on DESC,occurred_at DESC,id DESC
LIMIT 50;

\echo 'Bounded expired carryover entitlement scan'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT DISTINCT carry.employment_id,carry.leave_type_id
FROM business_hr.leave_balance_movement carry
WHERE carry.tenant_id=:'tenant_id'::uuid
  AND carry.legal_entity_id=:'legal_entity_id'::uuid
  AND carry.movement_type='CarryOver'
  AND carry.quantity>0
  AND carry.expires_on<=date '2025-01-01'
  AND NOT EXISTS (
      SELECT 1
      FROM business_hr.leave_balance_allocation allocation
      JOIN business_hr.leave_balance_movement expiry
        ON expiry.tenant_id=allocation.tenant_id AND expiry.id=allocation.movement_id
      WHERE allocation.tenant_id=carry.tenant_id
        AND allocation.entitlement_movement_id=carry.id
        AND expiry.movement_type='Expiry'
        AND expiry.source_type='LeaveEntitlementExpiry'
  )
ORDER BY carry.employment_id,carry.leave_type_id
LIMIT 5000;

\echo 'Carryover expiry replay detection'
EXPLAIN (ANALYZE, BUFFERS, SETTINGS)
SELECT allocation.entitlement_movement_id
FROM business_hr.leave_balance_movement entitlement
JOIN business_hr.leave_balance_allocation allocation
  ON allocation.tenant_id=entitlement.tenant_id
  AND allocation.entitlement_movement_id=entitlement.id
JOIN business_hr.leave_balance_movement expiry
  ON expiry.tenant_id=allocation.tenant_id
  AND expiry.id=allocation.movement_id
WHERE entitlement.tenant_id=:'tenant_id'::uuid
  AND entitlement.legal_entity_id=:'legal_entity_id'::uuid
  AND entitlement.movement_type='CarryOver'
  AND entitlement.quantity>0
  AND entitlement.expires_on<=date '2025-01-01'
  AND expiry.movement_type='Expiry'
  AND expiry.source_type='LeaveEntitlementExpiry';

RESET ROLE;
