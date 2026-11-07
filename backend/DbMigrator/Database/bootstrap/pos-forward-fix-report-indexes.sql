CREATE INDEX IF NOT EXISTS ix_pos_sale_report_order ON business_pos.pos_sale(tenant_id, business_date DESC, id DESC);
CREATE INDEX IF NOT EXISTS ix_pos_return_report_order ON business_pos.pos_return(tenant_id, created_at DESC, id DESC);
