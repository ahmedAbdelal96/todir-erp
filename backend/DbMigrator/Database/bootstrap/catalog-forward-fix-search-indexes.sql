-- Keep indexes aligned to actual Catalog predicates, joins, ordering, and exact lookups.
-- Tenant RLS blocks the trigram predicates from using the synthetic search indexes.
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_product_search;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_display_name_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_brand_name_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_name_ar_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_name_en_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_sku_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_category_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_alias_normalized_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_text_normalized_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_tenant_text_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_translation_name_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_barcode_normalized_trgm;

CREATE INDEX IF NOT EXISTS ix_catalog_search_category_page
    ON foundation_catalog.catalog_search_projection (tenant_id, category_id, status, updated_at DESC, variant_id);
CREATE INDEX IF NOT EXISTS ix_catalog_search_template_variant
    ON foundation_catalog.catalog_search_projection (tenant_id, template_id, variant_id);
CREATE INDEX IF NOT EXISTS ix_catalog_template_brand_lookup
    ON foundation_catalog.product_template (tenant_id, brand_id, id)
    WHERE brand_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_catalog_search_category_name_page
    ON foundation_catalog.catalog_search_projection (tenant_id, category_id, display_name_normalized, normalized_sku, variant_id);
