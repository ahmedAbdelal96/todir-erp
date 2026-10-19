ALTER TABLE foundation_catalog.catalog_search_projection
    ADD COLUMN IF NOT EXISTS search_text_normalized text
    GENERATED ALWAYS AS (lower(coalesce(display_name_normalized, '') || ' ' || coalesce(name_ar, '') || ' ' || coalesce(name_en, '') || ' ' || coalesce(sku, '') || ' ' || coalesce(category_name, ''))) STORED;

DROP INDEX IF EXISTS foundation_catalog.ix_catalog_product_search;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_display_name_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_name_ar_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_name_en_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_sku_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_category_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_search_tenant_name_ar_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_brand_name_trgm;
DROP INDEX IF EXISTS foundation_catalog.ix_catalog_translation_name_trgm;
