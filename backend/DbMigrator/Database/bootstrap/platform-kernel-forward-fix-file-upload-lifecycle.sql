ALTER TABLE platform.file_record
    ADD COLUMN upload_object_key text;

CREATE UNIQUE INDEX ux_platform_file_record_upload_key
    ON platform.file_record(upload_object_key)
    WHERE upload_object_key IS NOT NULL;

CREATE FUNCTION platform.guard_file_record_lifecycle() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
       OR NEW.owner_module IS DISTINCT FROM OLD.owner_module
       OR NEW.owner_entity_type IS DISTINCT FROM OLD.owner_entity_type
       OR NEW.owner_entity_id IS DISTINCT FROM OLD.owner_entity_id
       OR NEW.object_key IS DISTINCT FROM OLD.object_key
       OR NEW.upload_object_key IS DISTINCT FROM OLD.upload_object_key
       OR NEW.original_name IS DISTINCT FROM OLD.original_name
       OR NEW.content_type IS DISTINCT FROM OLD.content_type
       OR NEW.classification IS DISTINCT FROM OLD.classification
       OR NEW.created_by IS DISTINCT FROM OLD.created_by
       OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_metadata_immutable';
    END IF;

    IF OLD.status='Uploading' AND NEW.status='Quarantined' THEN
        IF NEW.size<>OLD.size OR NEW.sha256 !~ '^[0-9a-f]{64}$' THEN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_upload_evidence_invalid';
        END IF;
    ELSIF OLD.status='Uploading' AND NEW.status IN ('Rejected') THEN
        IF NEW.size<>OLD.size OR NEW.sha256 IS DISTINCT FROM OLD.sha256 THEN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_upload_evidence_invalid';
        END IF;
    ELSIF OLD.status='Quarantined' AND NEW.status IN ('Scanned','Rejected') THEN
        IF NEW.size<>OLD.size OR NEW.sha256 IS DISTINCT FROM OLD.sha256 THEN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_upload_evidence_invalid';
        END IF;
    ELSIF OLD.status='Scanned' AND NEW.status='Ready' THEN
        IF NEW.size<>OLD.size OR NEW.sha256 IS DISTINCT FROM OLD.sha256 THEN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_upload_evidence_invalid';
        END IF;
    ELSE
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='platform.file_status_transition_invalid';
    END IF;

    RETURN NEW;
END $$;

CREATE TRIGGER trg_platform_file_record_lifecycle
    BEFORE UPDATE ON platform.file_record
    FOR EACH ROW EXECUTE FUNCTION platform.guard_file_record_lifecycle();
