-- Remove the legacy source-module constraint left behind by the original
-- project-source forward fix, whose case-sensitive discovery missed it.
alter table business_invoicing.invoice
  drop constraint if exists invoice_source_module_check;
