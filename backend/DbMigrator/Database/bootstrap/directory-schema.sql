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
-- Name: foundation_directory; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA foundation_directory;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: address; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.address (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    country_code text NOT NULL,
    city text,
    region text,
    postal_code text,
    line1 text NOT NULL,
    line2 text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);



--
-- Name: contact_person; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.contact_person (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    job_title text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);



--
-- Name: contact_point; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.contact_point (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid,
    contact_person_id uuid,
    contact_type text NOT NULL,
    value text NOT NULL,
    normalized_value text NOT NULL,
    label text,
    is_primary boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT contact_point_check CHECK (((party_id IS NOT NULL) <> (contact_person_id IS NOT NULL))),
    CONSTRAINT contact_point_contact_type_check CHECK ((contact_type = ANY (ARRAY['Email'::text, 'Phone'::text, 'Website'::text, 'Other'::text])))
);



--
-- Name: duplicate_candidate; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.duplicate_candidate (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    candidate_party_id uuid NOT NULL,
    reason text NOT NULL,
    confidence numeric(5,4) NOT NULL,
    status text DEFAULT 'Open'::text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    resolved_at timestamp with time zone,
    CONSTRAINT duplicate_candidate_check CHECK ((party_id <> candidate_party_id))
);



--
-- Name: external_reference; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.external_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    system_code text NOT NULL,
    external_key text NOT NULL,
    created_at timestamp with time zone NOT NULL
);



--
-- Name: party; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_type text NOT NULL,
    status text NOT NULL,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    legal_name text,
    trade_name text,
    name_ar text,
    name_en text,
    first_name text,
    middle_name text,
    last_name text,
    country_code text,
    primary_language text,
    created_by uuid NOT NULL,
    updated_by uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT party_display_name_check CHECK (((length(display_name) >= 1) AND (length(display_name) <= 300))),
    CONSTRAINT party_party_type_check CHECK ((party_type = ANY (ARRAY['Person'::text, 'Organization'::text]))),
    CONSTRAINT party_status_check CHECK ((status = ANY (ARRAY['Draft'::text, 'Active'::text, 'Blocked'::text, 'Archived'::text, 'Merged'::text])))
);



--
-- Name: party_address; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_address (
    tenant_id uuid NOT NULL,
    party_id uuid NOT NULL,
    address_id uuid NOT NULL,
    usage text NOT NULL,
    is_default boolean DEFAULT false NOT NULL,
    CONSTRAINT party_address_usage_check CHECK ((usage = ANY (ARRAY['General'::text, 'Billing'::text, 'Shipping'::text, 'Registered'::text])))
);



--
-- Name: party_alias; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_alias (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    alias text NOT NULL,
    alias_normalized text NOT NULL,
    alias_type text,
    created_at timestamp with time zone NOT NULL
);



--
-- Name: party_contact; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_contact (
    tenant_id uuid NOT NULL,
    party_id uuid NOT NULL,
    contact_person_id uuid NOT NULL,
    relationship_type text,
    is_primary boolean DEFAULT false NOT NULL
);



--
-- Name: party_identifier; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_identifier (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    party_id uuid NOT NULL,
    identifier_type text NOT NULL,
    country_code text,
    value text NOT NULL,
    normalized_value text NOT NULL,
    authority text,
    valid_from date,
    valid_to date,
    is_primary boolean DEFAULT false NOT NULL,
    verification_status text DEFAULT 'Unverified'::text NOT NULL,
    created_at timestamp with time zone NOT NULL
);



--
-- Name: party_merge_redirect; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_merge_redirect (
    tenant_id uuid NOT NULL,
    source_party_id uuid NOT NULL,
    target_party_id uuid NOT NULL,
    merged_at timestamp with time zone NOT NULL,
    merged_by uuid NOT NULL
);



--
-- Name: party_relationship; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_relationship (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    from_party_id uuid NOT NULL,
    to_party_id uuid NOT NULL,
    relationship_type text NOT NULL,
    valid_from date,
    valid_to date,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT party_relationship_check CHECK ((from_party_id <> to_party_id))
);



--
-- Name: party_role; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_role (
    tenant_id uuid NOT NULL,
    party_id uuid NOT NULL,
    role_code text NOT NULL,
    status text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT party_role_role_code_check CHECK ((role_code = ANY (ARRAY['Customer'::text, 'Supplier'::text]))),
    CONSTRAINT party_role_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Inactive'::text])))
);



--
-- Name: party_visibility; Type: TABLE; Schema: foundation_directory; Owner: -
--

CREATE TABLE foundation_directory.party_visibility (
    tenant_id uuid NOT NULL,
    party_id uuid NOT NULL,
    scope_type text NOT NULL,
    legal_entity_id uuid,
    branch_id uuid,
    visibility_id uuid NOT NULL,
    CONSTRAINT party_visibility_scope_type_check CHECK ((scope_type = ANY (ARRAY['TenantWide'::text, 'LegalEntity'::text, 'Branch'::text])))
);



--
-- Name: address address_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.address
    ADD CONSTRAINT address_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: contact_person contact_person_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.contact_person
    ADD CONSTRAINT contact_person_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: contact_point contact_point_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.contact_point
    ADD CONSTRAINT contact_point_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: duplicate_candidate duplicate_candidate_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.duplicate_candidate
    ADD CONSTRAINT duplicate_candidate_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: external_reference external_reference_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.external_reference
    ADD CONSTRAINT external_reference_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: external_reference external_reference_tenant_id_system_code_external_key_key; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.external_reference
    ADD CONSTRAINT external_reference_tenant_id_system_code_external_key_key UNIQUE (tenant_id, system_code, external_key);


--
-- Name: party_address party_address_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_address
    ADD CONSTRAINT party_address_pkey PRIMARY KEY (tenant_id, party_id, address_id, usage);


--
-- Name: party_alias party_alias_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_alias
    ADD CONSTRAINT party_alias_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: party_contact party_contact_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_contact
    ADD CONSTRAINT party_contact_pkey PRIMARY KEY (tenant_id, party_id, contact_person_id);


--
-- Name: party_identifier party_identifier_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_identifier
    ADD CONSTRAINT party_identifier_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: party_identifier party_identifier_tenant_id_party_id_identifier_type_normali_key; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_identifier
    ADD CONSTRAINT party_identifier_tenant_id_party_id_identifier_type_normali_key UNIQUE (tenant_id, party_id, identifier_type, normalized_value);


--
-- Name: party_merge_redirect party_merge_redirect_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_merge_redirect
    ADD CONSTRAINT party_merge_redirect_pkey PRIMARY KEY (tenant_id, source_party_id);


--
-- Name: party party_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party
    ADD CONSTRAINT party_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: party_relationship party_relationship_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_relationship
    ADD CONSTRAINT party_relationship_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: party_role party_role_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_role
    ADD CONSTRAINT party_role_pkey PRIMARY KEY (tenant_id, party_id, role_code);


--
-- Name: party_visibility party_visibility_pkey; Type: CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_visibility
    ADD CONSTRAINT party_visibility_pkey PRIMARY KEY (tenant_id, visibility_id);


--
-- Name: ix_directory_contact_lookup; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_contact_lookup ON foundation_directory.contact_point USING btree (tenant_id, contact_type, normalized_value);


--
-- Name: ix_directory_duplicate_party; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_duplicate_party ON foundation_directory.duplicate_candidate USING btree (tenant_id, party_id, status);


--
-- Name: ix_directory_identifier_lookup; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_identifier_lookup ON foundation_directory.party_identifier USING btree (tenant_id, identifier_type, normalized_value);


--
-- Name: ix_directory_party_list; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_party_list ON foundation_directory.party USING btree (tenant_id, status, updated_at DESC, id);


--
-- Name: ix_directory_party_name; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_party_name ON foundation_directory.party USING gin (display_name_normalized public.gin_trgm_ops);


--
-- Name: ix_directory_role_lookup; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE INDEX ix_directory_role_lookup ON foundation_directory.party_role USING btree (tenant_id, role_code, status, party_id);


--
-- Name: ux_directory_party_visibility_scope; Type: INDEX; Schema: foundation_directory; Owner: -
--

CREATE UNIQUE INDEX ux_directory_party_visibility_scope ON foundation_directory.party_visibility USING btree (tenant_id, party_id, scope_type, COALESCE(legal_entity_id, '00000000-0000-0000-0000-000000000000'::uuid), COALESCE(branch_id, '00000000-0000-0000-0000-000000000000'::uuid));


--
-- Name: contact_point contact_point_tenant_id_contact_person_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.contact_point
    ADD CONSTRAINT contact_point_tenant_id_contact_person_id_fkey FOREIGN KEY (tenant_id, contact_person_id) REFERENCES foundation_directory.contact_person(tenant_id, id);


--
-- Name: contact_point contact_point_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.contact_point
    ADD CONSTRAINT contact_point_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: duplicate_candidate duplicate_candidate_tenant_id_candidate_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.duplicate_candidate
    ADD CONSTRAINT duplicate_candidate_tenant_id_candidate_party_id_fkey FOREIGN KEY (tenant_id, candidate_party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: duplicate_candidate duplicate_candidate_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.duplicate_candidate
    ADD CONSTRAINT duplicate_candidate_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: external_reference external_reference_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.external_reference
    ADD CONSTRAINT external_reference_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_address party_address_tenant_id_address_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_address
    ADD CONSTRAINT party_address_tenant_id_address_id_fkey FOREIGN KEY (tenant_id, address_id) REFERENCES foundation_directory.address(tenant_id, id);


--
-- Name: party_address party_address_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_address
    ADD CONSTRAINT party_address_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_alias party_alias_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_alias
    ADD CONSTRAINT party_alias_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_contact party_contact_tenant_id_contact_person_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_contact
    ADD CONSTRAINT party_contact_tenant_id_contact_person_id_fkey FOREIGN KEY (tenant_id, contact_person_id) REFERENCES foundation_directory.contact_person(tenant_id, id);


--
-- Name: party_contact party_contact_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_contact
    ADD CONSTRAINT party_contact_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_identifier party_identifier_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_identifier
    ADD CONSTRAINT party_identifier_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_merge_redirect party_merge_redirect_tenant_id_source_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_merge_redirect
    ADD CONSTRAINT party_merge_redirect_tenant_id_source_party_id_fkey FOREIGN KEY (tenant_id, source_party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_merge_redirect party_merge_redirect_tenant_id_target_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_merge_redirect
    ADD CONSTRAINT party_merge_redirect_tenant_id_target_party_id_fkey FOREIGN KEY (tenant_id, target_party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_relationship party_relationship_tenant_id_from_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_relationship
    ADD CONSTRAINT party_relationship_tenant_id_from_party_id_fkey FOREIGN KEY (tenant_id, from_party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_relationship party_relationship_tenant_id_to_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_relationship
    ADD CONSTRAINT party_relationship_tenant_id_to_party_id_fkey FOREIGN KEY (tenant_id, to_party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party_role party_role_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_role
    ADD CONSTRAINT party_role_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: party party_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party
    ADD CONSTRAINT party_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: party_visibility party_visibility_tenant_id_party_id_fkey; Type: FK CONSTRAINT; Schema: foundation_directory; Owner: -
--

ALTER TABLE ONLY foundation_directory.party_visibility
    ADD CONSTRAINT party_visibility_tenant_id_party_id_fkey FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id);


--
-- Name: address; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.address ENABLE ROW LEVEL SECURITY;

--
-- Name: contact_person; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.contact_person ENABLE ROW LEVEL SECURITY;

--
-- Name: contact_point; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.contact_point ENABLE ROW LEVEL SECURITY;

--
-- Name: duplicate_candidate; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.duplicate_candidate ENABLE ROW LEVEL SECURITY;

--
-- Name: external_reference; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.external_reference ENABLE ROW LEVEL SECURITY;

--
-- Name: party; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party ENABLE ROW LEVEL SECURITY;

--
-- Name: party_address; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_address ENABLE ROW LEVEL SECURITY;

--
-- Name: party_alias; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_alias ENABLE ROW LEVEL SECURITY;

--
-- Name: party_contact; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_contact ENABLE ROW LEVEL SECURITY;

--
-- Name: party_identifier; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_identifier ENABLE ROW LEVEL SECURITY;

--
-- Name: party_merge_redirect; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_merge_redirect ENABLE ROW LEVEL SECURITY;

--
-- Name: party_relationship; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_relationship ENABLE ROW LEVEL SECURITY;

--
-- Name: party_role; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_role ENABLE ROW LEVEL SECURITY;

--
-- Name: party_visibility; Type: ROW SECURITY; Schema: foundation_directory; Owner: -
--

ALTER TABLE foundation_directory.party_visibility ENABLE ROW LEVEL SECURITY;

--
-- Name: address tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.address USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: contact_person tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.contact_person USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: contact_point tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.contact_point USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: duplicate_candidate tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.duplicate_candidate USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: external_reference tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.external_reference USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_address tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_address USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_alias tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_alias USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_contact tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_contact USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_identifier tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_identifier USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_merge_redirect tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_merge_redirect USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_relationship tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_relationship USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_role tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_role USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: party_visibility tenant_isolation; Type: POLICY; Schema: foundation_directory; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_directory.party_visibility USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: SCHEMA foundation_directory; Type: ACL; Schema: -; Owner: -
--

GRANT USAGE ON SCHEMA foundation_directory TO erp_app;


--
-- Name: TABLE address; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.address TO erp_app;


--
-- Name: TABLE contact_person; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.contact_person TO erp_app;


--
-- Name: TABLE contact_point; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.contact_point TO erp_app;


--
-- Name: TABLE duplicate_candidate; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.duplicate_candidate TO erp_app;


--
-- Name: TABLE external_reference; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.external_reference TO erp_app;


--
-- Name: TABLE party; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party TO erp_app;


--
-- Name: TABLE party_address; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_address TO erp_app;


--
-- Name: TABLE party_alias; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_alias TO erp_app;


--
-- Name: TABLE party_contact; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_contact TO erp_app;


--
-- Name: TABLE party_identifier; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_identifier TO erp_app;


--
-- Name: TABLE party_merge_redirect; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_merge_redirect TO erp_app;


--
-- Name: TABLE party_relationship; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_relationship TO erp_app;


--
-- Name: TABLE party_role; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_role TO erp_app;


--
-- Name: TABLE party_visibility; Type: ACL; Schema: foundation_directory; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE foundation_directory.party_visibility TO erp_app;


--
-- PostgreSQL database dump complete
--

