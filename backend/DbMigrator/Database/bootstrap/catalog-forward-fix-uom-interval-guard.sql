CREATE OR REPLACE FUNCTION foundation_catalog.enforce_product_uom_interval()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.status <> 'Active' THEN
        RETURN NEW;
    END IF;

    PERFORM pg_advisory_xact_lock(
        hashtextextended(NEW.tenant_id::text || ':' || NEW.variant_id::text || ':' || NEW.uom_id::text, 0)
    );

    IF EXISTS (
        SELECT 1
        FROM foundation_catalog.product_uom existing
        WHERE existing.tenant_id = NEW.tenant_id
          AND existing.variant_id = NEW.variant_id
          AND existing.uom_id = NEW.uom_id
          AND existing.status = 'Active'
          AND existing.id <> NEW.id
          AND tstzrange(existing.effective_from, existing.effective_to, '[)')
              && tstzrange(NEW.effective_from, NEW.effective_to, '[)')
    ) THEN
        RAISE EXCEPTION 'active product UOM effective periods cannot overlap'
            USING ERRCODE = '23P01', CONSTRAINT = 'product_uom_period_no_overlap';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS product_uom_interval_guard ON foundation_catalog.product_uom;
CREATE TRIGGER product_uom_interval_guard
    BEFORE INSERT OR UPDATE OF tenant_id, variant_id, uom_id, effective_from, effective_to, status
    ON foundation_catalog.product_uom
    FOR EACH ROW
    EXECUTE FUNCTION foundation_catalog.enforce_product_uom_interval();

ALTER FUNCTION foundation_catalog.enforce_product_uom_interval() OWNER TO erp_owner;
GRANT EXECUTE ON FUNCTION foundation_catalog.enforce_product_uom_interval() TO erp_app;
