-- Projects is a valid receivable source for invoice drafts and must be visible in finalization events.
do $$
declare constraint_name text;
begin
  select con.conname into constraint_name
  from pg_constraint con
  join pg_class rel on rel.oid = con.conrelid
  join pg_namespace ns on ns.oid = rel.relnamespace
  where ns.nspname = 'business_invoicing'
    and rel.relname = 'invoice'
    and con.contype = 'c'
    and pg_get_constraintdef(con.oid) like '%source_module IS NULL OR source_module IN%';
  if constraint_name is not null then
    execute format('alter table business_invoicing.invoice drop constraint %I', constraint_name);
  end if;
end $$;

alter table business_invoicing.invoice
  add constraint ck_invoice_source_module_allowed
  check (source_module is null or source_module in ('sales','purchasing','pos','projects'));
