--
-- PostgreSQL database dump
--

-- Dumped from database version 17.0
-- Dumped by pg_dump version 17.0

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: foundation_catalog; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA foundation_catalog;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: brand; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.brand (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    name text NOT NULL,
    name_normalized text NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT brand_name_check CHECK (((length(name) >= 1) AND (length(name) <= 200))),
    CONSTRAINT brand_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: catalog_search_projection; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.catalog_search_projection (
    tenant_id uuid NOT NULL,
    variant_id uuid NOT NULL,
    template_id uuid NOT NULL,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    name_ar text,
    name_en text,
    sku text,
    normalized_sku text,
    category_id uuid,
    category_name text,
    base_uom_code text,
    product_nature text NOT NULL,
    status text NOT NULL,
    updated_at timestamp with time zone NOT NULL
);



--
-- Name: product_alias; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_alias (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    variant_id uuid NOT NULL,
    alias text NOT NULL,
    normalized_alias text NOT NULL,
    alias_type text NOT NULL,
    language_code text,
    created_at timestamp with time zone NOT NULL
);



--
-- Name: product_barcode; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_barcode (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    variant_id uuid NOT NULL,
    uom_id uuid,
    barcode_type text NOT NULL,
    value text NOT NULL,
    normalized_value text NOT NULL,
    is_primary boolean NOT NULL,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT product_barcode_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_category; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_category (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    name text NOT NULL,
    name_normalized text NOT NULL,
    parent_id uuid,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT product_category_name_check CHECK (((length(name) >= 1) AND (length(name) <= 200))),
    CONSTRAINT product_category_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_reference; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    variant_id uuid NOT NULL,
    reference_type text NOT NULL,
    party_id uuid,
    reference_code text NOT NULL,
    status text NOT NULL,
    CONSTRAINT product_reference_reference_type_check CHECK ((reference_type = ANY (ARRAY['Customer'::text, 'Supplier'::text, 'External'::text]))),
    CONSTRAINT product_reference_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_template; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_template (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    name_ar text,
    name_en text,
    product_nature text NOT NULL,
    category_id uuid,
    brand_id uuid,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT product_template_display_name_check CHECK (((length(display_name) >= 1) AND (length(display_name) <= 300))),
    CONSTRAINT product_template_product_nature_check CHECK ((product_nature = ANY (ARRAY['Goods'::text, 'Service'::text]))),
    CONSTRAINT product_template_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_translation; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_translation (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    template_id uuid NOT NULL,
    language_code text NOT NULL,
    name text NOT NULL,
    description text
);



--
-- Name: product_uom; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_uom (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    variant_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    factor_to_base numeric(19,6) NOT NULL,
    is_base boolean NOT NULL,
    effective_from timestamp with time zone NOT NULL,
    effective_to timestamp with time zone,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT product_uom_check CHECK (((effective_to IS NULL) OR (effective_to > effective_from))),
    CONSTRAINT product_uom_check1 CHECK (((is_base AND (factor_to_base = (1)::numeric)) OR (NOT is_base))),
    CONSTRAINT product_uom_factor_to_base_check CHECK ((factor_to_base > (0)::numeric)),
    CONSTRAINT product_uom_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_variant; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_variant (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    product_template_id uuid NOT NULL,
    sku text,
    normalized_sku text,
    is_default_variant boolean NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT product_variant_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: product_visibility; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.product_visibility (
    tenant_id uuid NOT NULL,
    template_id uuid NOT NULL,
    scope_type text NOT NULL,
    legal_entity_id uuid,
    visibility_id uuid DEFAULT gen_random_uuid() NOT NULL,
    CONSTRAINT product_visibility_scope_type_check CHECK ((scope_type = ANY (ARRAY['TenantWide'::text, 'LegalEntity'::text])))
);



--
-- Name: uom; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.uom (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    dimension text NOT NULL,
    allow_fraction boolean NOT NULL,
    "precision" smallint NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT uom_code_check CHECK (((length(code) >= 1) AND (length(code) <= 32))),
    CONSTRAINT uom_dimension_check CHECK ((dimension = ANY (ARRAY['Count'::text, 'Mass'::text, 'Length'::text, 'Area'::text, 'Volume'::text, 'Time'::text, 'BusinessUnit'::text]))),
    CONSTRAINT uom_precision_check CHECK ((("precision" >= 0) AND ("precision" <= 6))),
    CONSTRAINT uom_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: uom_dimension; Type: TABLE; Schema: foundation_catalog; Owner: -
--

CREATE TABLE foundation_catalog.uom_dimension (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    status text NOT NULL,
    CONSTRAINT uom_dimension_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: brand brand_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.brand
    ADD CONSTRAINT brand_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: brand brand_tenant_id_name_normalized_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.brand
    ADD CONSTRAINT brand_tenant_id_name_normalized_key UNIQUE (tenant_id, name_normalized);


--
-- Name: catalog_search_projection catalog_search_projection_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.catalog_search_projection
    ADD CONSTRAINT catalog_search_projection_pkey PRIMARY KEY (tenant_id, variant_id);


--
-- Name: product_alias product_alias_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_alias
    ADD CONSTRAINT product_alias_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_alias product_alias_tenant_id_variant_id_normalized_alias_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_alias
    ADD CONSTRAINT product_alias_tenant_id_variant_id_normalized_alias_key UNIQUE (tenant_id, variant_id, normalized_alias);


--
-- Name: product_barcode product_barcode_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_barcode
    ADD CONSTRAINT product_barcode_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_barcode product_barcode_tenant_id_normalized_value_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_barcode
    ADD CONSTRAINT product_barcode_tenant_id_normalized_value_key UNIQUE (tenant_id, normalized_value);


--
-- Name: product_category product_category_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_category
    ADD CONSTRAINT product_category_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_category product_category_tenant_id_name_normalized_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_category
    ADD CONSTRAINT product_category_tenant_id_name_normalized_key UNIQUE (tenant_id, name_normalized);


--
-- Name: product_reference product_reference_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_reference
    ADD CONSTRAINT product_reference_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_reference product_reference_tenant_id_reference_type_party_id_referen_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_reference
    ADD CONSTRAINT product_reference_tenant_id_reference_type_party_id_referen_key UNIQUE (tenant_id, reference_type, party_id, reference_code);


--
-- Name: product_template product_template_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_template
    ADD CONSTRAINT product_template_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_translation product_translation_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_translation
    ADD CONSTRAINT product_translation_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_translation product_translation_tenant_id_template_id_language_code_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_translation
    ADD CONSTRAINT product_translation_tenant_id_template_id_language_code_key UNIQUE (tenant_id, template_id, language_code);


--
-- Name: product_uom product_uom_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_uom
    ADD CONSTRAINT product_uom_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_variant product_variant_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_variant
    ADD CONSTRAINT product_variant_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: product_variant product_variant_tenant_id_normalized_sku_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_variant
    ADD CONSTRAINT product_variant_tenant_id_normalized_sku_key UNIQUE (tenant_id, normalized_sku);


--
-- Name: product_visibility product_visibility_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_visibility
    ADD CONSTRAINT product_visibility_pkey PRIMARY KEY (tenant_id, visibility_id);


--
-- Name: uom_dimension uom_dimension_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom_dimension
    ADD CONSTRAINT uom_dimension_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: uom_dimension uom_dimension_tenant_id_code_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom_dimension
    ADD CONSTRAINT uom_dimension_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: uom uom_pkey; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom
    ADD CONSTRAINT uom_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: uom uom_tenant_id_code_key; Type: CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom
    ADD CONSTRAINT uom_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: ix_catalog_barcode; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_barcode ON foundation_catalog.product_barcode USING btree (tenant_id, normalized_value) WHERE (status = 'Active'::text);


--
-- Name: ix_catalog_category; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_category ON foundation_catalog.product_template USING btree (tenant_id, category_id, status);


--
-- Name: ix_catalog_product_list; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_product_list ON foundation_catalog.catalog_search_projection USING btree (tenant_id, status, updated_at DESC, variant_id);


--
-- Name: ix_catalog_product_search; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_product_search ON foundation_catalog.catalog_search_projection USING gin (display_name_normalized public.gin_trgm_ops);


--
-- Name: ix_catalog_sku; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_sku ON foundation_catalog.product_variant USING btree (tenant_id, normalized_sku) WHERE (normalized_sku IS NOT NULL);


--
-- Name: ix_catalog_uom; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE INDEX ix_catalog_uom ON foundation_catalog.product_uom USING btree (tenant_id, variant_id, uom_id, status, effective_from DESC);


--
-- Name: ux_catalog_one_current_base; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE UNIQUE INDEX ux_catalog_one_current_base ON foundation_catalog.product_uom USING btree (tenant_id, variant_id) WHERE (is_base AND (status = 'Active'::text) AND (effective_to IS NULL));


--
-- Name: ux_catalog_visibility_scope; Type: INDEX; Schema: foundation_catalog; Owner: -
--

CREATE UNIQUE INDEX ux_catalog_visibility_scope ON foundation_catalog.product_visibility USING btree (tenant_id, template_id, scope_type, COALESCE(legal_entity_id, '00000000-0000-0000-0000-000000000000'::uuid));


--
-- Name: brand brand_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.brand
    ADD CONSTRAINT brand_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: catalog_search_projection catalog_search_projection_tenant_id_variant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.catalog_search_projection
    ADD CONSTRAINT catalog_search_projection_tenant_id_variant_id_fkey FOREIGN KEY (tenant_id, variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id);


--
-- Name: product_alias product_alias_tenant_id_variant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_alias
    ADD CONSTRAINT product_alias_tenant_id_variant_id_fkey FOREIGN KEY (tenant_id, variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id);


--
-- Name: product_barcode product_barcode_tenant_id_uom_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_barcode
    ADD CONSTRAINT product_barcode_tenant_id_uom_id_fkey FOREIGN KEY (tenant_id, uom_id) REFERENCES foundation_catalog.uom(tenant_id, id);


--
-- Name: product_barcode product_barcode_tenant_id_variant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_barcode
    ADD CONSTRAINT product_barcode_tenant_id_variant_id_fkey FOREIGN KEY (tenant_id, variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id);


--
-- Name: product_category product_category_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_category
    ADD CONSTRAINT product_category_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: product_category product_category_tenant_id_parent_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_category
    ADD CONSTRAINT product_category_tenant_id_parent_id_fkey FOREIGN KEY (tenant_id, parent_id) REFERENCES foundation_catalog.product_category(tenant_id, id);


--
-- Name: product_reference product_reference_tenant_id_variant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_reference
    ADD CONSTRAINT product_reference_tenant_id_variant_id_fkey FOREIGN KEY (tenant_id, variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id);


--
-- Name: product_template product_template_tenant_id_brand_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_template
    ADD CONSTRAINT product_template_tenant_id_brand_id_fkey FOREIGN KEY (tenant_id, brand_id) REFERENCES foundation_catalog.brand(tenant_id, id);


--
-- Name: product_template product_template_tenant_id_category_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_template
    ADD CONSTRAINT product_template_tenant_id_category_id_fkey FOREIGN KEY (tenant_id, category_id) REFERENCES foundation_catalog.product_category(tenant_id, id);


--
-- Name: product_template product_template_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_template
    ADD CONSTRAINT product_template_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: product_translation product_translation_tenant_id_template_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_translation
    ADD CONSTRAINT product_translation_tenant_id_template_id_fkey FOREIGN KEY (tenant_id, template_id) REFERENCES foundation_catalog.product_template(tenant_id, id);


--
-- Name: product_uom product_uom_tenant_id_uom_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_uom
    ADD CONSTRAINT product_uom_tenant_id_uom_id_fkey FOREIGN KEY (tenant_id, uom_id) REFERENCES foundation_catalog.uom(tenant_id, id);


--
-- Name: product_uom product_uom_tenant_id_variant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_uom
    ADD CONSTRAINT product_uom_tenant_id_variant_id_fkey FOREIGN KEY (tenant_id, variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id);


--
-- Name: product_variant product_variant_tenant_id_product_template_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_variant
    ADD CONSTRAINT product_variant_tenant_id_product_template_id_fkey FOREIGN KEY (tenant_id, product_template_id) REFERENCES foundation_catalog.product_template(tenant_id, id);


--
-- Name: product_visibility product_visibility_tenant_id_template_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.product_visibility
    ADD CONSTRAINT product_visibility_tenant_id_template_id_fkey FOREIGN KEY (tenant_id, template_id) REFERENCES foundation_catalog.product_template(tenant_id, id);


--
-- Name: uom_dimension uom_dimension_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom_dimension
    ADD CONSTRAINT uom_dimension_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: uom uom_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_catalog; Owner: -
--

ALTER TABLE ONLY foundation_catalog.uom
    ADD CONSTRAINT uom_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: brand; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.brand ENABLE ROW LEVEL SECURITY;

--
-- Name: catalog_search_projection; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.catalog_search_projection ENABLE ROW LEVEL SECURITY;

--
-- Name: product_alias; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_alias ENABLE ROW LEVEL SECURITY;

--
-- Name: product_barcode; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_barcode ENABLE ROW LEVEL SECURITY;

--
-- Name: product_category; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_category ENABLE ROW LEVEL SECURITY;

--
-- Name: product_reference; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_reference ENABLE ROW LEVEL SECURITY;

--
-- Name: product_template; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_template ENABLE ROW LEVEL SECURITY;

--
-- Name: product_translation; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_translation ENABLE ROW LEVEL SECURITY;

--
-- Name: product_uom; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_uom ENABLE ROW LEVEL SECURITY;

--
-- Name: product_variant; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_variant ENABLE ROW LEVEL SECURITY;

--
-- Name: product_visibility; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.product_visibility ENABLE ROW LEVEL SECURITY;

--
-- Name: brand tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.brand USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: catalog_search_projection tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.catalog_search_projection USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_alias tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_alias USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_barcode tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_barcode USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_category tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_category USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_reference tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_reference USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_template tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_template USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_translation tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_translation USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_uom tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_uom USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_variant tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_variant USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: product_visibility tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.product_visibility USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: uom tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.uom USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: uom_dimension tenant_isolation; Type: POLICY; Schema: foundation_catalog; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_catalog.uom_dimension USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: uom; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.uom ENABLE ROW LEVEL SECURITY;

--
-- Name: uom_dimension; Type: ROW SECURITY; Schema: foundation_catalog; Owner: -
--

ALTER TABLE foundation_catalog.uom_dimension ENABLE ROW LEVEL SECURITY;

--
-- Name: SCHEMA foundation_catalog; Type: ACL; Schema: -; Owner: -
--

GRANT USAGE ON SCHEMA foundation_catalog TO erp_app;


--
-- Name: TABLE brand; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.brand TO erp_app;


--
-- Name: TABLE catalog_search_projection; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.catalog_search_projection TO erp_app;


--
-- Name: TABLE product_alias; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_alias TO erp_app;


--
-- Name: TABLE product_barcode; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_barcode TO erp_app;


--
-- Name: TABLE product_category; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_category TO erp_app;


--
-- Name: TABLE product_reference; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_reference TO erp_app;


--
-- Name: TABLE product_template; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_template TO erp_app;


--
-- Name: TABLE product_translation; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_translation TO erp_app;


--
-- Name: TABLE product_uom; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_uom TO erp_app;


--
-- Name: TABLE product_variant; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_variant TO erp_app;


--
-- Name: TABLE product_visibility; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.product_visibility TO erp_app;


--
-- Name: TABLE uom; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.uom TO erp_app;


--
-- Name: TABLE uom_dimension; Type: ACL; Schema: foundation_catalog; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_catalog.uom_dimension TO erp_app;


--
-- PostgreSQL database dump complete
--

