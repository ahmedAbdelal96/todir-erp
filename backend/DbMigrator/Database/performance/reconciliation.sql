with item_balance as (
  select i.id,i.direction,i.polarity,i.control_account_role,i.control_account_reference,i.control_configuration_reference,i.base_currency,
    i.original_base_amount as original_amount
  from finance_subledger.party_open_item i
  where i.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and i.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and i.posting_date<=date '2026-10-25'
), application_effects as (
  select item_id,sum(amount) amount from (
    select a.debit_item_id item_id,a.debit_base_carrying_amount_closed amount
    from finance_subledger.open_item_application a
    where a.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and a.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and a.application_date<=date '2026-10-25'
    union all
    select a.credit_item_id item_id,a.credit_base_carrying_amount_closed amount
    from finance_subledger.open_item_application a
    where a.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and a.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and a.application_date<=date '2026-10-25'
  ) lines group by item_id
), reversal_effects as (
  select item_id,sum(amount) amount from (
    select a.debit_item_id item_id,a.debit_base_carrying_amount_closed amount
    from finance_subledger.application_reversal r
    join finance_subledger.open_item_application a on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
    where r.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and r.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and r.effective_date<=date '2026-10-25'
    union all
    select a.credit_item_id item_id,a.credit_base_carrying_amount_closed amount
    from finance_subledger.application_reversal r
    join finance_subledger.open_item_application a on a.tenant_id=r.tenant_id and a.legal_entity_id=r.legal_entity_id and a.id=r.original_application_id
    where r.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and r.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and r.effective_date<=date '2026-10-25'
  ) lines group by item_id
), write_off_effects as (
  select w.open_item_id item_id,sum(w.base_amount) amount
  from finance_subledger.write_off w
  where w.tenant_id='c9fea3c8-249d-f150-a53a-84f5a891c82b' and w.legal_entity_id='aad8a34b-344c-9e5b-8d58-57f182fc6528' and w.effective_date<=date '2026-10-25'
  group by w.open_item_id
), balances as (
  select i.*,a.amount applied_amount,r.amount reversed_amount,w.amount written_off_amount
  from item_balance i
  left join application_effects a on a.item_id=i.id
  left join reversal_effects r on r.item_id=i.id
  left join write_off_effects w on w.item_id=i.id
)
select direction,control_account_role,control_account_reference,control_configuration_reference,base_currency,
  sum(case when (direction='Receivable' and polarity='Debit') or (direction='Payable' and polarity='Credit')
    then original_amount-coalesce(applied_amount,0)+coalesce(reversed_amount,0)-coalesce(written_off_amount,0)
    else -(original_amount-coalesce(applied_amount,0)+coalesce(reversed_amount,0)-coalesce(written_off_amount,0)) end)
from balances group by direction,control_account_role,control_configuration_reference,control_account_reference,base_currency
order by direction,control_account_role,control_configuration_reference,control_account_reference;
