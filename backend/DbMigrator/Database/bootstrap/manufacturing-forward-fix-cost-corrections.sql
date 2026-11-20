-- Returns and approved cost corrections may reduce WIP; retain the signed economic fact.
alter table business_manufacturing.production_actual_cost_fact
  drop constraint if exists production_actual_cost_fact_amount_check;
