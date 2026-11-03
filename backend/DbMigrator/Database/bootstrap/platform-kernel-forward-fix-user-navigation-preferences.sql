CREATE TABLE platform.user_navigation_state (
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    navigation_key text NOT NULL,
    favorite_at timestamptz,
    last_visited_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT user_navigation_state_pkey PRIMARY KEY (tenant_id, identity_id, navigation_key),
    CONSTRAINT user_navigation_state_navigation_key_check CHECK (navigation_key ~ '^[a-z][a-z0-9-]{0,63}$'),
    CONSTRAINT user_navigation_state_has_state_check CHECK (favorite_at IS NOT NULL OR last_visited_at IS NOT NULL),
    CONSTRAINT user_navigation_state_identity_fkey FOREIGN KEY (tenant_id, identity_id)
        REFERENCES platform.identity (tenant_id, id) ON DELETE CASCADE
);

CREATE INDEX user_navigation_state_favorites_idx
    ON platform.user_navigation_state (tenant_id, identity_id, favorite_at DESC, navigation_key)
    WHERE favorite_at IS NOT NULL;

CREATE INDEX user_navigation_state_recent_idx
    ON platform.user_navigation_state (tenant_id, identity_id, last_visited_at DESC, navigation_key)
    WHERE last_visited_at IS NOT NULL;

ALTER TABLE platform.user_navigation_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.user_navigation_state FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON platform.user_navigation_state
    USING (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid)
    WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.user_navigation_state TO erp_app;
