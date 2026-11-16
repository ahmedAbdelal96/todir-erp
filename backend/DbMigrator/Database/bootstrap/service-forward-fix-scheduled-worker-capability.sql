reset role;
grant usage on schema business_service to erp_dispatcher;
grant select on business_service.maintenance_plan, business_service.sla_clock, business_service.work_order to erp_dispatcher;
set role erp_owner;
