-- Service creates customer invoice drafts through the same source contract as
-- Sales, POS and Projects. Keep both legacy and canonical constraints aligned.
alter table business_invoicing.invoice
  drop constraint if exists ck_invoice_source_module_allowed,
  drop constraint if exists invoice_source_module_check;

alter table business_invoicing.invoice
  add constraint ck_invoice_source_module_allowed
    check (source_module is null or source_module in ('sales','purchasing','pos','projects','service')),
  add constraint invoice_source_module_check
    check (source_module is null or source_module in ('sales','purchasing','pos','projects','service'));
