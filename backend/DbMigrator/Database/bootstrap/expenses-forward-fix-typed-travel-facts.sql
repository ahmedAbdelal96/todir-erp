-- Persist typed travel facts required to reproduce mileage/per-diem amounts.
alter table business_expenses.mileage_expense_detail add column if not exists round_trip boolean not null default false;
alter table business_expenses.mileage_expense_detail add column if not exists calculated_distance numeric(19,4);
alter table business_expenses.mileage_expense_detail add column if not exists calculated_amount numeric(19,4);
update business_expenses.mileage_expense_detail
   set calculated_distance = distance,
       calculated_amount = round(distance * rate, 4)
 where calculated_distance is null or calculated_amount is null;
alter table business_expenses.mileage_expense_detail alter column calculated_distance set not null;
alter table business_expenses.mileage_expense_detail alter column calculated_amount set not null;

alter table business_expenses.per_diem_expense_detail add column if not exists trip_start_at timestamptz;
alter table business_expenses.per_diem_expense_detail add column if not exists trip_end_at timestamptz;
update business_expenses.per_diem_expense_detail p
   set trip_start_at = coalesce(p.trip_start_at, e.expense_date::timestamp at time zone 'UTC'),
       trip_end_at = coalesce(p.trip_end_at, (e.expense_date + greatest(1, ceil(p.days)::integer - 1))::timestamp at time zone 'UTC')
  from business_expenses.expense e
 where e.tenant_id = p.tenant_id
   and e.id = p.expense_id
   and (p.trip_start_at is null or p.trip_end_at is null);

create index if not exists ix_expenses_typed_travel_policy on business_expenses.mileage_expense_detail(tenant_id,legal_entity_id,rate_policy_version_id);
create index if not exists ix_expenses_typed_per_diem_policy on business_expenses.per_diem_expense_detail(tenant_id,legal_entity_id,rate_policy_version_id);
