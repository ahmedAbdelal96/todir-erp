ALTER TABLE business_hr.legal_entity_go_live_configuration
    DROP CONSTRAINT legal_entity_go_live_configur_document_migration_strategy_check;
ALTER TABLE business_hr.legal_entity_go_live_configuration
    ADD CONSTRAINT ck_hr_golive_doc_strategy
        CHECK (document_migration_strategy IN ('None','AllAvailable'));
