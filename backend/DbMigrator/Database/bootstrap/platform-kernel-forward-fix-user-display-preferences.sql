CREATE TABLE IF NOT EXISTS platform.user_display_preferences (
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    language text NOT NULL DEFAULT 'ar',
    theme text NOT NULL DEFAULT 'default',
    density text NOT NULL DEFAULT 'comfortable',
    sidebar_collapsed boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT user_display_preferences_pkey PRIMARY KEY (tenant_id, identity_id),
    CONSTRAINT user_display_preferences_language_check CHECK (language IN ('ar', 'en')),
    CONSTRAINT user_display_preferences_theme_check CHECK (theme IN ('default', 'dark')),
    CONSTRAINT user_display_preferences_density_check CHECK (density IN ('comfortable', 'compact')),
    CONSTRAINT user_display_preferences_identity_fkey FOREIGN KEY (tenant_id, identity_id)
        REFERENCES platform.identity (tenant_id, id) ON DELETE CASCADE
);

ALTER TABLE platform.user_display_preferences ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.user_display_preferences FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON platform.user_display_preferences;
CREATE POLICY tenant_isolation ON platform.user_display_preferences
    USING (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid)
    WITH CHECK (tenant_id = (NULLIF(current_setting('app.tenant_id', true), ''))::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.user_display_preferences TO erp_app;
