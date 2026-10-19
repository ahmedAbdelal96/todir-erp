ALTER TABLE foundation_finbase.financial_rounding_policy
    ADD COLUMN IF NOT EXISTS line_document_strategy text NOT NULL DEFAULT 'RoundEachLineThenSum';

ALTER TABLE foundation_finbase.financial_rounding_policy
    DROP CONSTRAINT IF EXISTS financial_rounding_policy_line_document_strategy_check;

ALTER TABLE foundation_finbase.financial_rounding_policy
    ADD CONSTRAINT financial_rounding_policy_line_document_strategy_check
        CHECK (line_document_strategy IN ('RoundEachLineThenSum', 'SumExactLinesThenRound'));

CREATE TABLE IF NOT EXISTS foundation_finbase.financial_rounding_policy_rule (
    tenant_id uuid NOT NULL,
    rounding_policy_id uuid NOT NULL,
    rounding_context text NOT NULL,
    mode text NOT NULL,
    CONSTRAINT financial_rounding_policy_rule_pk PRIMARY KEY (tenant_id, rounding_policy_id, rounding_context),
    CONSTRAINT financial_rounding_policy_rule_parent_fk FOREIGN KEY (tenant_id, rounding_policy_id)
        REFERENCES foundation_finbase.financial_rounding_policy (tenant_id, id),
    CONSTRAINT financial_rounding_policy_rule_context_check CHECK (rounding_context IN (
        'MoneySettlement', 'LineNetAmount', 'DocumentSubtotal', 'DocumentTotal', 'FXBaseAmount',
        'PaymentScheduleLine', 'PaymentAllocation', 'CashRounding'
    )),
    CONSTRAINT financial_rounding_policy_rule_mode_check CHECK (mode IN ('HalfUp', 'HalfEven', 'Down', 'Up'))
);

INSERT INTO foundation_finbase.financial_rounding_policy_rule (tenant_id, rounding_policy_id, rounding_context, mode)
SELECT policy.tenant_id, policy.id, context.rounding_context, policy.mode
  FROM foundation_finbase.financial_rounding_policy AS policy
 CROSS JOIN (VALUES
     ('MoneySettlement'), ('LineNetAmount'), ('DocumentSubtotal'), ('DocumentTotal'), ('FXBaseAmount'),
     ('PaymentScheduleLine'), ('PaymentAllocation'), ('CashRounding')
 ) AS context(rounding_context)
ON CONFLICT (tenant_id, rounding_policy_id, rounding_context) DO NOTHING;

ALTER TABLE foundation_finbase.financial_rounding_policy_rule ENABLE ROW LEVEL SECURITY;
ALTER TABLE foundation_finbase.financial_rounding_policy_rule FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON foundation_finbase.financial_rounding_policy_rule;
CREATE POLICY tenant_isolation ON foundation_finbase.financial_rounding_policy_rule
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON TABLE foundation_finbase.financial_rounding_policy_rule TO erp_app;

CREATE OR REPLACE FUNCTION foundation_finbase.protect_rounding_policy_rule_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.rounding_policy_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_rounding_policy_rule_history ON foundation_finbase.financial_rounding_policy_rule;
CREATE TRIGGER trg_finbase_protect_rounding_policy_rule_history
    BEFORE UPDATE OR DELETE ON foundation_finbase.financial_rounding_policy_rule
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_rounding_policy_rule_history();
