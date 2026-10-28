CREATE INDEX IF NOT EXISTS reservation_expiry_due_idx
  ON business_inventory.reservation(expires_at,tenant_id,legal_entity_id)
  WHERE status='Active' AND expires_at IS NOT NULL;

CREATE OR REPLACE FUNCTION business_inventory.due_reservation_scopes(limit_count integer, cutoff timestamptz DEFAULT now())
RETURNS TABLE(tenant_id uuid, legal_entity_id uuid)
LANGUAGE sql
SECURITY DEFINER
SET search_path TO 'business_inventory','pg_catalog'
SET row_security TO 'on'
AS $$
  SELECT DISTINCT reservation.tenant_id,reservation.legal_entity_id
    FROM business_inventory.reservation AS reservation
   WHERE reservation.status='Active'
     AND reservation.expires_at IS NOT NULL
     AND reservation.expires_at<=cutoff
   ORDER BY reservation.tenant_id,reservation.legal_entity_id
   LIMIT LEAST(GREATEST(limit_count,1),500)
$$;

REVOKE ALL ON FUNCTION business_inventory.due_reservation_scopes(integer,timestamptz) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION business_inventory.due_reservation_scopes(integer,timestamptz) TO erp_app;
GRANT USAGE,CREATE ON SCHEMA business_inventory TO erp_dispatcher;
GRANT SELECT ON business_inventory.reservation TO erp_dispatcher;
