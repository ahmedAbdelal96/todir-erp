-- Module 26 ledger contract for databases upgraded after the original seed.
-- This is intentionally a forward fix: it is safe to apply after the
-- request-hash migration and does not alter existing posted entries.
set row_security = off;
alter table finance_ledger.ledger_entity_configuration no force row level security;
alter table finance_ledger.account_category no force row level security;
alter table finance_ledger.account no force row level security;
alter table finance_ledger.account_role no force row level security;
alter table finance_ledger.account_mapping no force row level security;
alter table finance_ledger.account_mapping_version no force row level security;
alter table finance_ledger.accounting_configuration_version no force row level security;
alter table finance_ledger.fact_producer_registration no force row level security;
alter table finance_ledger.posting_rule no force row level security;
alter table finance_ledger.posting_rule_version no force row level security;
alter table finance_ledger.posting_rule_leg no force row level security;
do $$
declare
  entity record;
  definition text[];
  category_id uuid;
  parent_id uuid;
  account_id uuid;
  role_id uuid;
  target_mapping_id uuid;
  rule_id uuid;
  rule_version_id uuid;
begin
  for entity in
    select tenant_id, legal_entity_id, active_configuration_version_id
      from finance_ledger.ledger_entity_configuration
  loop
    foreach definition slice 1 in array array[
      array['140000','Asset','ManufacturingWip','أعمال تحت التنفيذ التصنيعية','Manufacturing work in progress','Debit','ASSET'],
      array['240000','Liability','ManufacturingAccrual','استحقاق تكاليف التصنيع','Manufacturing cost accrual','Credit','LIABILITY'],
      array['551000','Expense','ManufacturingVariance','فروق تكاليف التصنيع','Manufacturing cost variance','Debit','EXPENSE']
    ]
    loop
      select id into category_id
        from finance_ledger.account_category
       where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id
         and code=definition[7] and status='Active'
       limit 1;
      if category_id is null then continue; end if;
      parent_id := null;
      if definition[1] = '140000' then
        select id into parent_id from finance_ledger.account
         where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id
           and code='110000' and status='Active' limit 1;
      end if;
      insert into finance_ledger.account
        (tenant_id,legal_entity_id,id,category_id,code,name_ar,name_en,
         account_type,subtype,normal_balance,parent_account_id,allow_direct_posting,
         currency_policy,status,version,created_at,updated_at)
      values (entity.tenant_id,entity.legal_entity_id,
              md5(entity.tenant_id::text||entity.legal_entity_id::text||definition[1])::uuid,
              category_id,definition[1],definition[4],definition[5],definition[2],
              definition[3],definition[6],parent_id,true,'BaseOnly','Active',1,now(),now())
      on conflict (tenant_id,legal_entity_id,code) do nothing;
      select id into account_id from finance_ledger.account
       where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id and code=definition[1];

      insert into finance_ledger.account_role
        (tenant_id,legal_entity_id,id,code,name_ar,name_en,status,version)
      values (entity.tenant_id,entity.legal_entity_id,
              md5(entity.tenant_id::text||entity.legal_entity_id::text||definition[3]||'-role')::uuid,
              definition[3],definition[4],definition[5],'Active',1)
      on conflict (tenant_id,legal_entity_id,code) do nothing;
      select id into role_id from finance_ledger.account_role
       where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id and code=definition[3];
      insert into finance_ledger.account_mapping
        (tenant_id,legal_entity_id,id,role_id,code,status,version)
      values (entity.tenant_id,entity.legal_entity_id,
              md5(entity.tenant_id::text||entity.legal_entity_id::text||definition[3]||'-mapping')::uuid,
              role_id,definition[3],'Active',1)
      on conflict (tenant_id,legal_entity_id,code) do nothing;
      select id into target_mapping_id from finance_ledger.account_mapping
       where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id and code=definition[3];
      insert into finance_ledger.account_mapping_version
        (tenant_id,legal_entity_id,id,mapping_id,version_number,effective_from,
         accounting_configuration_version_id,specificity_rank,target_account_id,status)
      select entity.tenant_id,entity.legal_entity_id,
             md5(entity.tenant_id::text||entity.legal_entity_id::text||definition[3]||'-version')::uuid,
             target_mapping_id,1,coalesce(c.effective_from,current_date),c.id,0,account_id,'Active'
        from finance_ledger.accounting_configuration_version c
       where c.tenant_id=entity.tenant_id and c.legal_entity_id=entity.legal_entity_id
         and c.id=entity.active_configuration_version_id
         and not exists (select 1 from finance_ledger.account_mapping_version v
                          where v.tenant_id=entity.tenant_id and v.mapping_id=target_mapping_id and v.version_number=1);
    end loop;

    insert into finance_ledger.fact_producer_registration
      (tenant_id,legal_entity_id,id,producer_module,fact_type,fact_version,status,registered_at)
    values (entity.tenant_id,entity.legal_entity_id,
            md5(entity.tenant_id::text||entity.legal_entity_id::text||'manufacturing-producer')::uuid,
            'manufacturing','ManufacturingCostAccrued',1,'Active',now())
    on conflict (tenant_id,legal_entity_id,producer_module,fact_type,fact_version) do nothing;
    insert into finance_ledger.posting_rule
      (tenant_id,legal_entity_id,id,fact_type,fact_version,code,status,version,created_at)
    values (entity.tenant_id,entity.legal_entity_id,
            md5(entity.tenant_id::text||entity.legal_entity_id::text||'manufacturing-rule')::uuid,
            'ManufacturingCostAccrued',1,'MANUFACTURING_COST_ACCRUED_V1','Active',1,now())
    on conflict (tenant_id,legal_entity_id,fact_type,fact_version,code) do nothing;
    select id into rule_id from finance_ledger.posting_rule
     where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id
       and fact_type='ManufacturingCostAccrued' and fact_version=1
       and code='MANUFACTURING_COST_ACCRUED_V1';
    insert into finance_ledger.posting_rule_version
      (tenant_id,legal_entity_id,id,posting_rule_id,version_number,effective_from,status)
    values (entity.tenant_id,entity.legal_entity_id,
            md5(entity.tenant_id::text||entity.legal_entity_id::text||'manufacturing-rule-version')::uuid,
            rule_id,1,current_date,'Active')
    on conflict (tenant_id,posting_rule_id,version_number) do nothing;
    select id into rule_version_id from finance_ledger.posting_rule_version
     where tenant_id=entity.tenant_id and legal_entity_id=entity.legal_entity_id
       and posting_rule_id=rule_id and version_number=1;
    insert into finance_ledger.posting_rule_leg
      (tenant_id,legal_entity_id,id,posting_rule_version_id,sequence_no,semantic_role,amount_expression,direction)
    values
      (entity.tenant_id,entity.legal_entity_id,md5(entity.tenant_id::text||entity.legal_entity_id::text||'manufacturing-wip-leg')::uuid,rule_version_id,1,'ManufacturingWip','BaseAmount','Debit'),
      (entity.tenant_id,entity.legal_entity_id,md5(entity.tenant_id::text||entity.legal_entity_id::text||'manufacturing-accrual-leg')::uuid,rule_version_id,2,'ManufacturingAccrual','BaseAmount','Credit')
    on conflict (tenant_id,posting_rule_version_id,sequence_no) do nothing;
  end loop;
end $$;
alter table finance_ledger.ledger_entity_configuration force row level security;
alter table finance_ledger.account_category force row level security;
alter table finance_ledger.account force row level security;
alter table finance_ledger.account_role force row level security;
alter table finance_ledger.account_mapping force row level security;
alter table finance_ledger.account_mapping_version force row level security;
alter table finance_ledger.accounting_configuration_version force row level security;
alter table finance_ledger.fact_producer_registration force row level security;
alter table finance_ledger.posting_rule force row level security;
alter table finance_ledger.posting_rule_version force row level security;
alter table finance_ledger.posting_rule_leg force row level security;
