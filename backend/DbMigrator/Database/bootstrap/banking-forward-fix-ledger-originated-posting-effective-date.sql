SET row_security = off;

INSERT INTO finance_ledger.account_mapping_version
    (tenant_id, legal_entity_id, id, mapping_id, accounting_configuration_version_id, version_number,
     effective_from, effective_to, specificity_rank, target_account_id, status)
SELECT old.tenant_id, old.legal_entity_id,
       md5(old.tenant_id::text || ':' || old.mapping_id::text || ':historical-bank-originated-v2')::uuid,
       old.mapping_id, c.id, old.version_number + 1, c.effective_from, old.effective_from - 1,
       old.specificity_rank, old.target_account_id, 'Active'
FROM finance_ledger.account_mapping_version old
JOIN finance_ledger.account_mapping m ON m.tenant_id=old.tenant_id AND m.legal_entity_id=old.legal_entity_id AND m.id=old.mapping_id
JOIN finance_ledger.account_role r ON r.tenant_id=m.tenant_id AND r.legal_entity_id=m.legal_entity_id AND r.id=m.role_id
JOIN finance_ledger.ledger_entity_configuration lec ON lec.tenant_id=m.tenant_id AND lec.legal_entity_id=m.legal_entity_id
JOIN finance_ledger.accounting_configuration_version c ON c.tenant_id=lec.tenant_id AND c.legal_entity_id=lec.legal_entity_id AND c.id=lec.active_configuration_version_id
WHERE r.code IN ('BankFees','BankInterestIncome','BankInterestExpense','DirectDebitExpense')
  AND old.status='Active' AND old.effective_from > c.effective_from
ON CONFLICT (tenant_id, mapping_id, version_number) DO NOTHING;

RESET row_security;
RESET search_path;
