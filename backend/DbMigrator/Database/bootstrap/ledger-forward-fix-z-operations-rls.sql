DO $$ DECLARE table_name text; BEGIN
    FOREACH table_name IN ARRAY ARRAY['journal_entry_reversal','dimension_definition','dimension_value','dimension_policy','journal_line_dimension','general_journal_draft','general_journal_draft_line','general_journal_draft_line_dimension','opening_balance_batch','opening_balance_line','account_period_balance','journal_entry_line'] LOOP
        EXECUTE format('ALTER TABLE finance_ledger.%I FORCE ROW LEVEL SECURITY', table_name);
    END LOOP;
END $$;
