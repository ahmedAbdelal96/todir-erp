ALTER TABLE finance_ledger.ledger_entity_configuration NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.accounting_configuration_version NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_version NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_leg NO FORCE ROW LEVEL SECURITY;

INSERT INTO finance_ledger.fact_producer_registration
    (tenant_id, legal_entity_id, id, producer_module, fact_type, fact_version, status, registered_at)
SELECT configuration.tenant_id, configuration.legal_entity_id,
       md5(configuration.tenant_id::text || ':' || configuration.legal_entity_id::text || ':accounting:UnrealizedFxRevaluation:1')::uuid,
       'accounting', 'UnrealizedFxRevaluation', 1, 'Active', now()
FROM finance_ledger.ledger_entity_configuration configuration
ON CONFLICT (tenant_id, legal_entity_id, producer_module, fact_type, fact_version) DO NOTHING;

INSERT INTO finance_ledger.posting_rule
    (tenant_id, legal_entity_id, id, fact_type, fact_version, code, status, version, created_at)
SELECT configuration.tenant_id, configuration.legal_entity_id,
       md5(configuration.tenant_id::text || ':' || configuration.legal_entity_id::text || ':UNREALIZED_FX_REVALUATION_V1')::uuid,
       'UnrealizedFxRevaluation', 1, 'UNREALIZED_FX_REVALUATION_V1', 'Active', 1, now()
FROM finance_ledger.ledger_entity_configuration configuration
ON CONFLICT (tenant_id, legal_entity_id, fact_type, fact_version, code) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_version
    (tenant_id, legal_entity_id, id, posting_rule_id, version_number, effective_from, status)
SELECT rule.tenant_id, rule.legal_entity_id,
       md5(rule.tenant_id::text || ':' || rule.legal_entity_id::text || ':' || rule.code || ':1')::uuid,
       rule.id, 1, configuration.effective_from, 'Active'
FROM finance_ledger.posting_rule rule
JOIN finance_ledger.ledger_entity_configuration entity_configuration
  ON entity_configuration.tenant_id = rule.tenant_id AND entity_configuration.legal_entity_id = rule.legal_entity_id
JOIN finance_ledger.accounting_configuration_version configuration
  ON configuration.tenant_id = entity_configuration.tenant_id
 AND configuration.legal_entity_id = entity_configuration.legal_entity_id
 AND configuration.id = entity_configuration.active_configuration_version_id
WHERE rule.code = 'UNREALIZED_FX_REVALUATION_V1'
ON CONFLICT (tenant_id, posting_rule_id, version_number) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_leg
    (tenant_id, legal_entity_id, id, posting_rule_version_id, sequence_no, semantic_role, amount_expression, direction)
SELECT rule_version.tenant_id, rule_version.legal_entity_id,
       md5(rule_version.tenant_id::text || ':' || rule_version.legal_entity_id::text || ':' || rule_version.id::text || ':' || leg.sequence_no)::uuid,
       rule_version.id, leg.sequence_no, leg.semantic_role, 'BaseAmount', leg.direction
FROM finance_ledger.posting_rule rule
JOIN finance_ledger.posting_rule_version rule_version
  ON rule_version.tenant_id = rule.tenant_id AND rule_version.legal_entity_id = rule.legal_entity_id
 AND rule_version.posting_rule_id = rule.id AND rule_version.version_number = 1
CROSS JOIN (VALUES (1, 'Receivable', 'Debit'), (2, 'Payable', 'Credit'), (3, 'FXGain', 'Credit'), (4, 'FXLoss', 'Debit')) AS leg(sequence_no, semantic_role, direction)
WHERE rule.code = 'UNREALIZED_FX_REVALUATION_V1'
ON CONFLICT (tenant_id, posting_rule_version_id, sequence_no) DO NOTHING;

ALTER TABLE finance_ledger.ledger_entity_configuration FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.accounting_configuration_version FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_version FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_leg FORCE ROW LEVEL SECURITY;
