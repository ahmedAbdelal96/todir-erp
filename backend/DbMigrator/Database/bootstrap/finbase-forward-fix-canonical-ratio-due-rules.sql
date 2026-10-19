ALTER TABLE foundation_finbase.payment_term_rule
    DROP CONSTRAINT IF EXISTS payment_term_rule_due_date_basis_check;

UPDATE foundation_finbase.payment_term_rule
SET percentage = percentage / 100
WHERE allocation_type = 'Percentage'
  AND percentage > 1
  AND percentage <= 100;

UPDATE foundation_finbase.payment_term_rule
SET due_date_basis = CASE due_date_basis
    WHEN 'InvoiceDate' THEN 'DaysAfterDate'
    WHEN 'DeliveryDate' THEN 'DaysAfterDate'
    WHEN 'MonthEnd' THEN 'EndOfMonth'
    ELSE due_date_basis
END
WHERE due_date_basis IN ('InvoiceDate', 'DeliveryDate', 'MonthEnd');

ALTER TABLE foundation_finbase.payment_term_rule
    ADD CONSTRAINT payment_term_rule_percentage_ratio_check
        CHECK (allocation_type <> 'Percentage' OR (percentage >= 0 AND percentage <= 1)),
    ADD CONSTRAINT payment_term_rule_due_date_basis_check
        CHECK (due_date_basis = ANY (ARRAY[
            'Immediate'::text,
            'DaysAfterDate'::text,
            'EndOfMonth'::text,
            'DaysAfterEndOfMonth'::text,
            'DayOfFollowingMonth'::text
        ]));
