ALTER TABLE business_hr.legal_entity_go_live_configuration
    ADD COLUMN leave_history_boundary date;
ALTER TABLE business_hr.legal_entity_go_live_configuration
    ADD CONSTRAINT ck_hr_golive_leave_history_before_go_live
        CHECK (leave_history_boundary IS NULL OR leave_history_boundary <= go_live_date);
