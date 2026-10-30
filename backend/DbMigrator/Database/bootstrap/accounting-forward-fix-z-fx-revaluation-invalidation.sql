SET row_security = off;

ALTER TABLE business_accounting.fx_revaluation_run
    DROP CONSTRAINT IF EXISTS fx_revaluation_run_status_check;
ALTER TABLE business_accounting.fx_revaluation_run
    ADD CONSTRAINT fx_revaluation_run_status_check
    CHECK (status IN ('Previewed','Posting','Posted','Reversed','Invalidated'));
