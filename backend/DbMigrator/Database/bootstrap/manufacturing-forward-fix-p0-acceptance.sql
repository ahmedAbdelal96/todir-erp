-- Module 26 P0 correction: persist the single MRP netting decision so the
-- explosion pass and supply pass cannot deduct the same stock twice.
set row_security = off;
alter table business_manufacturing.planning_demand no force row level security;

alter table business_manufacturing.planning_demand
  add column if not exists stock_allocated_qty numeric(28,10) not null default 0,
  add column if not exists uncovered_qty numeric(28,10) not null default 0;

update business_manufacturing.planning_demand
set uncovered_qty = quantity
where uncovered_qty = 0 and quantity > 0;

alter table business_manufacturing.planning_demand
  add constraint planning_demand_stock_allocated_check
    check (stock_allocated_qty >= 0 and stock_allocated_qty <= quantity),
  add constraint planning_demand_uncovered_check
    check (uncovered_qty >= 0 and uncovered_qty <= quantity);

alter table business_manufacturing.planning_demand force row level security;
