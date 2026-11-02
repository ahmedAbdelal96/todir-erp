ALTER TABLE business_hr.employment_contract ALTER COLUMN start_date DROP NOT NULL;
CREATE OR REPLACE FUNCTION business_hr.enforce_active_contract_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.status = 'Active' THEN
        PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text || ':contracts', 0));
        IF EXISTS (
            SELECT 1 FROM business_hr.employment_contract c
            WHERE c.tenant_id=NEW.tenant_id AND c.employment_id=NEW.employment_id AND c.id<>NEW.id AND c.status='Active'
              AND (NEW.start_date IS NULL OR c.start_date IS NULL
                   OR (NEW.start_date <= COALESCE(c.end_date,'infinity'::date)
                       AND c.start_date <= COALESCE(NEW.end_date,'infinity'::date)))
        ) THEN RAISE EXCEPTION USING ERRCODE='23P01', MESSAGE='hr.contract_active_dates_overlap'; END IF;
    END IF;
    RETURN NEW;
END $$;
