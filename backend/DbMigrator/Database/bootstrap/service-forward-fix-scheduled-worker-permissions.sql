reset role;
grant usage on schema business_service to erp_app;
grant execute on function business_service.list_scheduled_scopes() to erp_app;
set role erp_owner;
