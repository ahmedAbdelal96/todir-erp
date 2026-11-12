INSERT INTO finance_ledger.fact_producer_registration
    (tenant_id, legal_entity_id, id, producer_module, fact_type, fact_version, status, registered_at)
SELECT configuration.tenant_id, configuration.legal_entity_id,
       md5(configuration.tenant_id::text || ':' || configuration.legal_entity_id::text || ':fixed-assets:FixedAssetDisposalReversed:1')::uuid,
       'fixed-assets', 'FixedAssetDisposalReversed', 1, 'Active', now()
FROM finance_ledger.ledger_entity_configuration configuration
ON CONFLICT (tenant_id, legal_entity_id, producer_module, fact_type, fact_version) DO NOTHING;

INSERT INTO finance_ledger.posting_rule
    (tenant_id, legal_entity_id, id, fact_type, fact_version, code, status, version, created_at)
SELECT configuration.tenant_id, configuration.legal_entity_id,
       md5(configuration.tenant_id::text || ':' || configuration.legal_entity_id::text || ':FIXED_ASSET_DISPOSAL_REVERSED_V1')::uuid,
       'FixedAssetDisposalReversed', 1, 'FIXED_ASSET_DISPOSAL_REVERSED_V1', 'Active', 1, now()
FROM finance_ledger.ledger_entity_configuration configuration
ON CONFLICT (tenant_id, legal_entity_id, fact_type, fact_version, code) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_version
    (tenant_id, legal_entity_id, id, posting_rule_id, version_number, effective_from, status)
SELECT rule.tenant_id, rule.legal_entity_id,
       md5(rule.tenant_id::text || ':' || rule.legal_entity_id::text || ':' || rule.code || ':1')::uuid,
       rule.id, 1, current_date, 'Active'
FROM finance_ledger.posting_rule rule
WHERE rule.code = 'FIXED_ASSET_DISPOSAL_REVERSED_V1'
ON CONFLICT (tenant_id, posting_rule_id, version_number) DO NOTHING;

INSERT INTO finance_ledger.posting_rule_leg
    (tenant_id, legal_entity_id, id, posting_rule_version_id, sequence_no, semantic_role, amount_expression, direction)
SELECT version.tenant_id, version.legal_entity_id,
       md5(version.tenant_id::text || ':' || version.legal_entity_id::text || ':FIXED_ASSET_DISPOSAL_REVERSED_V1:' || leg.sequence_no::text)::uuid,
       version.id, leg.sequence_no, leg.semantic_role, leg.amount_expression, leg.direction
FROM finance_ledger.posting_rule_version version
JOIN finance_ledger.posting_rule rule ON rule.tenant_id = version.tenant_id AND rule.legal_entity_id = version.legal_entity_id AND rule.id = version.posting_rule_id
CROSS JOIN (VALUES
    (1, 'FixedAssetAccumulatedDepreciation', 'BaseAmount', 'Credit'),
    (2, 'FixedAssetAccumulatedImpairment', 'BaseAmount', 'Credit'),
    (3, 'FixedAssetCost', 'GrossCost', 'Debit'),
    (4, 'Cash', 'Proceeds', 'Credit'),
    (5, 'Cash', 'DirectCosts', 'Debit'),
    (6, 'FixedAssetDisposalGain', 'GainLoss', 'Debit'),
    (7, 'FixedAssetDisposalLoss', 'GainLoss', 'Credit')
) leg(sequence_no, semantic_role, amount_expression, direction)
WHERE rule.code = 'FIXED_ASSET_DISPOSAL_REVERSED_V1' AND version.version_number = 1
ON CONFLICT (tenant_id, posting_rule_version_id, sequence_no) DO NOTHING;
ALTER TABLE finance_ledger.ledger_entity_configuration NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_version NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_leg NO FORCE ROW LEVEL SECURITY;


ALTER TABLE finance_ledger.ledger_entity_configuration FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fact_producer_registration FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_version FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_leg FORCE ROW LEVEL SECURITY;
