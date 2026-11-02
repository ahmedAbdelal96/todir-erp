CREATE OR REPLACE FUNCTION business_hr.protect_insurance_enrollment_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.employment_id<>OLD.employment_id OR NEW.legal_entity_id<>OLD.legal_entity_id
       OR NEW.scheme_code<>OLD.scheme_code OR NEW.provider_code IS DISTINCT FROM OLD.provider_code
       OR NEW.encrypted_member_number IS DISTINCT FROM OLD.encrypted_member_number OR NEW.masked_member_number IS DISTINCT FROM OLD.masked_member_number
       OR NEW.effective_from<>OLD.effective_from OR NEW.effective_to IS DISTINCT FROM OLD.effective_to
       OR NEW.country_provider_version IS DISTINCT FROM OLD.country_provider_version OR NEW.evidence_file_ref IS DISTINCT FROM OLD.evidence_file_ref
       OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.insurance_evidence_immutable';
    END IF;
    IF OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Active','Rejected') THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.insurance_status_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
