SET row_security = off;

INSERT INTO finance_ledger.posting_rule_version
    (tenant_id, legal_entity_id, id, posting_rule_id, version_number, effective_from, effective_to, status)
SELECT r.tenant_id, r.legal_entity_id,
       md5(r.tenant_id::text || ':' || r.legal_entity_id::text || ':BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V2')::uuid,
       r.id, 2, c.effective_from, old.effective_from - 1, 'Active'
FROM finance_ledger.posting_rule r
JOIN finance_ledger.posting_rule_version old ON old.tenant_id=r.tenant_id AND old.legal_entity_id=r.legal_entity_id AND old.posting_rule_id=r.id AND old.version_number=1
JOIN finance_ledger.ledger_entity_configuration lec ON lec.tenant_id=r.tenant_id AND lec.legal_entity_id=r.legal_entity_id
JOIN finance_ledger.accounting_configuration_version c ON c.tenant_id=lec.tenant_id AND c.legal_entity_id=lec.legal_entity_id AND c.id=lec.active_configuration_version_id
WHERE r.fact_type='BankOriginatedFinancialEventApproved' AND r.fact_version=1 AND old.effective_from > c.effective_from
ON CONFLICT (tenant_id, posting_rule_id, version_number) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_leg
    (tenant_id, legal_entity_id, id, posting_rule_version_id, sequence_no, semantic_role, amount_expression, direction)
SELECT v.tenant_id, v.legal_entity_id,
       md5(v.tenant_id::text || ':' || v.legal_entity_id::text || ':BANK_ORIGINATED_FINANCIAL_EVENT_APPROVED_V2:' || x.sequence_no::text)::uuid,
       v.id, x.sequence_no, x.semantic_role, 'BaseAmount', x.direction
FROM finance_ledger.posting_rule_version v
JOIN finance_ledger.posting_rule r ON r.tenant_id=v.tenant_id AND r.legal_entity_id=v.legal_entity_id AND r.id=v.posting_rule_id
CROSS JOIN (VALUES (1,'BankOriginatedOffset','Debit'),(2,'Cash','Credit')) AS x(sequence_no,semantic_role,direction)
WHERE r.fact_type='BankOriginatedFinancialEventApproved' AND r.fact_version=1 AND v.version_number=2
ON CONFLICT (tenant_id, posting_rule_version_id, sequence_no) DO NOTHING;

RESET row_security;
RESET search_path;
