-- Module 27 bounded operational reads.
create index if not exists ix_quality_disposition_list
    on business_quality.quality_disposition (tenant_id, legal_entity_id, created_at desc, id desc);

create index if not exists ix_quality_certificate_list
    on business_quality.quality_certificate (tenant_id, legal_entity_id, created_at desc, id desc);

create index if not exists ix_quality_supplier_profile_list
    on business_quality.supplier_quality_profile (tenant_id, legal_entity_id, id);

create index if not exists ix_quality_scar_list
    on business_quality.supplier_corrective_action_request (tenant_id, legal_entity_id, id);

create index if not exists ix_quality_capa_action_due
    on business_quality.capa_action (tenant_id, capa_id, state, due_date);
