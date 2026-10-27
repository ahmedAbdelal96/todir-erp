INSERT INTO finance_ledger.posting_rule_leg
    (tenant_id, legal_entity_id, id, posting_rule_version_id, sequence_no, semantic_role, amount_expression, direction)
SELECT version.tenant_id, version.legal_entity_id,
       md5(version.tenant_id::text || ':' || version.legal_entity_id::text || ':PAYMENT_FEE_RECOGNIZED_V1:' || leg.sequence_no::text)::uuid,
       version.id, leg.sequence_no, leg.semantic_role, 'BaseAmount', leg.direction
FROM finance_ledger.posting_rule_version version
JOIN finance_ledger.posting_rule rule
  ON rule.tenant_id = version.tenant_id AND rule.legal_entity_id = version.legal_entity_id AND rule.id = version.posting_rule_id
CROSS JOIN (VALUES (1, 'PaymentFeeExpense', 'Debit'), (2, 'Cash', 'Credit')) AS leg(sequence_no, semantic_role, direction)
WHERE rule.code = 'PAYMENT_FEE_RECOGNIZED_V1'
ON CONFLICT (tenant_id, posting_rule_version_id, sequence_no) DO NOTHING;
