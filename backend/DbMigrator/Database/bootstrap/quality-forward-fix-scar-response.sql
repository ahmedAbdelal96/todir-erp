alter table business_quality.supplier_corrective_action_request
    add column if not exists supplier_response text,
    add column if not exists response_evidence_reference text,
    add column if not exists responded_at timestamptz,
    add column if not exists reviewed_by uuid,
    add column if not exists reviewed_at timestamptz,
    add column if not exists review_notes text;
