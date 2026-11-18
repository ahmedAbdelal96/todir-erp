-- Adds the managed Ledger contract and semantic accounts required by approved bank-originated events.
SET row_security = off;
ALTER TABLE platform.legal_entity NO FORCE ROW LEVEL SECURITY;

INSERT INTO finance_ledger.account
    (tenant_id, legal_entity_id, id, category_id, code, name_ar, name_en, account_type, subtype, normal_balance,
     parent_account_id, allow_direct_posting, currency_policy, status, version, created_at, updated_at)
SELECT e.tenant_id, e.id, md5(e.tenant_id::text || ':' || e.id::text || ':' || x.code)::uuid,
       NULL, x.code, x.name_ar, x.name_en, x.account_type, x.subtype, x.normal_balance,
       p.id, true, 'BaseOnly', 'Active', 1, now(), now()
FROM platform.legal_entity e
CROSS JOIN (VALUES
    ('541000','مصروفات بنكية','Bank Fees','Expense','BankFees','Debit','500000'),
    ('542000','مصروف فوائد','Bank Interest Expense','Expense','BankInterestExpense','Debit','500000'),
    ('543000','مصروفات خصم مباشر','Direct Debit Expense','Expense','DirectDebitExpense','Debit','500000'),
    ('425000','إيرادات فوائد بنكية','Bank Interest Income','Revenue','BankInterestIncome','Credit','400000')
) AS x(code,name_ar,name_en,account_type,subtype,normal_balance,parent_code)
LEFT JOIN finance_ledger.account p
  ON p.tenant_id=e.tenant_id AND p.legal_entity_id=e.id AND p.code=x.parent_code
ON CONFLICT (tenant_id, legal_entity_id, code) DO NOTHING;

INSERT INTO finance_ledger.account_role (tenant_id, legal_entity_id, id, code, name_ar, name_en, status, version)
SELECT e.tenant_id, e.id, md5(e.tenant_id::text || ':' || e.id::text || ':role:' || x.code)::uuid,
       x.code, x.name_ar, x.name_en, 'Active', 1
FROM platform.legal_entity e
CROSS JOIN (VALUES
    ('BankFees','مصروفات بنكية','Bank Fees'),
    ('BankInterestIncome','إيرادات فوائد بنكية','Bank Interest Income'),
    ('BankInterestExpense','مصروف فوائد','Bank Interest Expense'),
    ('DirectDebitExpense','مصروفات خصم مباشر','Direct Debit Expense')
) AS x(code,name_ar,name_en)
ON CONFLICT (tenant_id, legal_entity_id, code) DO NOTHING;

INSERT INTO finance_ledger.account_mapping (tenant_id, legal_entity_id, id, role_id, code, status, version)
SELECT r.tenant_id, r.legal_entity_id, md5(r.tenant_id::text || ':' || r.legal_entity_id::text || ':mapping:' || r.code)::uuid,
       r.id, 'DEFAULT_' || r.code, 'Active', 1
FROM finance_ledger.account_role r
WHERE r.code IN ('BankFees','BankInterestIncome','BankInterestExpense','DirectDebitExpense')
ON CONFLICT (tenant_id, legal_entity_id, code) DO NOTHING;

INSERT INTO finance_ledger.account_mapping_version
    (tenant_id, legal_entity_id, id, mapping_id, accounting_configuration_version_id, version_number,
     effective_from, specificity_rank, target_account_id, status)
SELECT m.tenant_id, m.legal_entity_id,
       md5(m.tenant_id::text || ':' || m.legal_entity_id::text || ':mapping-version:' || r.code)::uuid,
       m.id, c.active_configuration_version_id, 1, current_date, 0, a.id, 'Active'
FROM finance_ledger.account_mapping m
JOIN finance_ledger.account_role r ON r.tenant_id=m.tenant_id AND r.legal_entity_id=m.legal_entity_id AND r.id=m.role_id
JOIN finance_ledger.ledger_entity_configuration c ON c.tenant_id=m.tenant_id AND c.legal_entity_id=m.legal_entity_id
JOIN finance_ledger.account a ON a.tenant_id=m.tenant_id AND a.legal_entity_id=m.legal_entity_id AND a.code = CASE r.code
    WHEN 'BankFees' THEN '541000' WHEN 'BankInterestIncome' THEN '425000'
    WHEN 'BankInterestExpense' THEN '542000' WHEN 'DirectDebitExpense' THEN '543000' END
WHERE r.code IN ('BankFees','BankInterestIncome','BankInterestExpense','DirectDebitExpense')
  AND c.active_configuration_version_id IS NOT NULL
ON CONFLICT (tenant_id, mapping_id, version_number) DO NOTHING;

INSERT INTO finance_ledger.fact_producer_registration
    (tenant_id, legal_entity_id, id, producer_module, fact_type, fact_version, status, registered_at)
SELECT e.tenant_id, e.id, md5(e.tenant_id::text || ':' || e.id::text || ':banking:BankOriginatedFinancialEventApproved:1')::uuid,
       'banking', 'BankOriginatedFinancialEventApproved', 1, 'Active', now()
FROM platform.legal_entity e
ON CONFLICT (tenant_id, legal_entity_id, producer_module, fact_type, fact_version) DO NOTHING;

INSERT INTO finance_ledger.posting_rule
    (tenant_id, legal_entity_id, id, fact_type, fact_version, code, status, version, created_at)
SELECT e.tenant_id, e.id, md5(e.tenant_id::text || ':' || e.id::text || ':BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V1')::uuid,
       'BankOriginatedFinancialEventApproved', 1, 'BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V1', 'Active', 1, now()
FROM platform.legal_entity e
ON CONFLICT (tenant_id, legal_entity_id, fact_type, fact_version, code) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_version
    (tenant_id, legal_entity_id, id, posting_rule_id, version_number, effective_from, status)
SELECT r.tenant_id, r.legal_entity_id, md5(r.tenant_id::text || ':' || r.legal_entity_id::text || ':BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V1:VERSION')::uuid,
       r.id, 1, current_date, 'Active'
FROM finance_ledger.posting_rule r
WHERE r.fact_type='BankOriginatedFinancialEventApproved' AND r.fact_version=1
ON CONFLICT (tenant_id, posting_rule_id, version_number) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_leg
    (tenant_id, legal_entity_id, id, posting_rule_version_id, sequence_no, semantic_role, amount_expression, direction)
SELECT v.tenant_id, v.legal_entity_id,
       md5(v.tenant_id::text || ':' || v.legal_entity_id::text || ':BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V1:' || x.sequence_no::text)::uuid,
       v.id, x.sequence_no, x.semantic_role, 'BaseAmount', x.direction
FROM finance_ledger.posting_rule_version v
JOIN finance_ledger.posting_rule r ON r.tenant_id=v.tenant_id AND r.legal_entity_id=v.legal_entity_id AND r.id=v.posting_rule_id
CROSS JOIN (VALUES (1,'BankOriginatedOffset','Debit'),(2,'Cash','Credit')) AS x(sequence_no,semantic_role,direction)
WHERE r.fact_type='BankOriginatedFinancialEventApproved' AND r.fact_version=1
ON CONFLICT (tenant_id, posting_rule_version_id, sequence_no) DO NOTHING;

RESET row_security;
