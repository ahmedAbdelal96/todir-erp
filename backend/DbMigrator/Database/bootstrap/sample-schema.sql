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
-- Name: sample; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA sample;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: sample_item; Type: TABLE; Schema: sample; Owner: -
--

CREATE TABLE sample.sample_item (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    name text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT sample_item_name_check CHECK (((length(name) >= 1) AND (length(name) <= 200)))
);



--
-- Name: sample_projection; Type: TABLE; Schema: sample; Owner: -
--

CREATE TABLE sample.sample_projection (
    tenant_id uuid NOT NULL,
    item_id uuid NOT NULL,
    projected_at timestamp with time zone NOT NULL,
    version bigint DEFAULT 1 NOT NULL
);



--
-- Name: sample_item sample_item_pkey; Type: CONSTRAINT; Schema: sample; Owner: -
--

ALTER TABLE ONLY sample.sample_item
    ADD CONSTRAINT sample_item_pkey PRIMARY KEY (id);


--
-- Name: sample_item sample_item_tenant_id_id_key; Type: CONSTRAINT; Schema: sample; Owner: -
--

ALTER TABLE ONLY sample.sample_item
    ADD CONSTRAINT sample_item_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: sample_projection sample_projection_pkey; Type: CONSTRAINT; Schema: sample; Owner: -
--

ALTER TABLE ONLY sample.sample_projection
    ADD CONSTRAINT sample_projection_pkey PRIMARY KEY (tenant_id, item_id);


--
-- Name: ix_sample_item_tenant_created; Type: INDEX; Schema: sample; Owner: -
--

CREATE INDEX ix_sample_item_tenant_created ON sample.sample_item USING btree (tenant_id, created_at DESC, id);


--
-- Name: ix_sample_projection_tenant_projected; Type: INDEX; Schema: sample; Owner: -
--

CREATE INDEX ix_sample_projection_tenant_projected ON sample.sample_projection USING btree (tenant_id, projected_at DESC, item_id);


--
-- Name: sample_item sample_item_tenant_id_fkey; Type: FK CONSTRAINT; Schema: sample; Owner: -
--

ALTER TABLE ONLY sample.sample_item
    ADD CONSTRAINT sample_item_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: sample_projection sample_projection_tenant_id_fkey; Type: FK CONSTRAINT; Schema: sample; Owner: -
--

ALTER TABLE ONLY sample.sample_projection
    ADD CONSTRAINT sample_projection_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: sample_item; Type: ROW SECURITY; Schema: sample; Owner: -
--

ALTER TABLE sample.sample_item ENABLE ROW LEVEL SECURITY;

--
-- Name: sample_projection; Type: ROW SECURITY; Schema: sample; Owner: -
--

ALTER TABLE sample.sample_projection ENABLE ROW LEVEL SECURITY;

--
-- Name: sample_item tenant_isolation; Type: POLICY; Schema: sample; Owner: -
--

CREATE POLICY tenant_isolation ON sample.sample_item USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: sample_projection tenant_isolation; Type: POLICY; Schema: sample; Owner: -
--

CREATE POLICY tenant_isolation ON sample.sample_projection USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: SCHEMA sample; Type: ACL; Schema: -; Owner: -
--

GRANT USAGE ON SCHEMA sample TO erp_app;


--
-- Name: TABLE sample_item; Type: ACL; Schema: sample; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE sample.sample_item TO erp_app;


--
-- Name: TABLE sample_projection; Type: ACL; Schema: sample; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE sample.sample_projection TO erp_app;


--
-- PostgreSQL database dump complete
--

