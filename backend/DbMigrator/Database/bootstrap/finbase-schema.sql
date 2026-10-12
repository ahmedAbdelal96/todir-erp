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
-- Name: foundation_finbase; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA foundation_finbase;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: currency_reference; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.currency_reference (
    code text NOT NULL,
    numeric_code smallint NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    minor_unit smallint NOT NULL,
    status text NOT NULL,
    valid_from date NOT NULL,
    valid_to date,
    dataset_version text NOT NULL,
    CONSTRAINT currency_reference_check CHECK (((valid_to IS NULL) OR (valid_to >= valid_from))),
    CONSTRAINT currency_reference_code_check CHECK ((code ~ '^[A-Z]{3}$'::text)),
    CONSTRAINT currency_reference_minor_unit_check CHECK (((minor_unit >= 0) AND (minor_unit <= 8))),
    CONSTRAINT currency_reference_numeric_code_check CHECK (((numeric_code >= 1) AND (numeric_code <= 999))),
    CONSTRAINT currency_reference_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Deprecated'::text])))
);


--
-- Name: exchange_rate; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.exchange_rate (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    from_currency text NOT NULL,
    to_currency text NOT NULL,
    rate numeric(28,12) NOT NULL,
    purpose text NOT NULL,
    effective_date date NOT NULL,
    source_type text NOT NULL,
    provider_reference text,
    status text NOT NULL,
    approved_at timestamp with time zone,
    approved_by uuid,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT exchange_rate_check CHECK ((from_currency <> to_currency)),
    CONSTRAINT exchange_rate_purpose_check CHECK ((purpose = ANY (ARRAY['Accounting'::text, 'Commercial'::text, 'Contractual'::text, 'BankActual'::text, 'Settlement'::text, 'Tax'::text]))),
    CONSTRAINT exchange_rate_rate_check CHECK ((rate > (0)::numeric)),
    CONSTRAINT exchange_rate_source_type_check CHECK ((source_type = ANY (ARRAY['Manual'::text, 'Provider'::text, 'Bank'::text, 'Contract'::text]))),
    CONSTRAINT exchange_rate_status_check CHECK ((status = ANY (ARRAY['Draft'::text, 'Approved'::text, 'Superseded'::text, 'Rejected'::text])))
);



--
-- Name: financial_policy; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.financial_policy (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    rounding_policy_id uuid NOT NULL,
    exchange_rate_max_age_days integer NOT NULL,
    manual_rate_override_allowed boolean DEFAULT false NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT financial_policy_exchange_rate_max_age_days_check CHECK (((exchange_rate_max_age_days >= 0) AND (exchange_rate_max_age_days <= 3650)))
);



--
-- Name: financial_rounding_policy; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.financial_rounding_policy (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    mode text NOT NULL,
    money_scale smallint NOT NULL,
    cash_increment numeric(28,8) NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT financial_rounding_policy_cash_increment_check CHECK ((cash_increment > (0)::numeric)),
    CONSTRAINT financial_rounding_policy_mode_check CHECK ((mode = ANY (ARRAY['HalfUp'::text, 'HalfEven'::text, 'Down'::text, 'Up'::text]))),
    CONSTRAINT financial_rounding_policy_money_scale_check CHECK (((money_scale >= 0) AND (money_scale <= 8))),
    CONSTRAINT financial_rounding_policy_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: legal_entity_currency; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.legal_entity_currency (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    currency_code text NOT NULL,
    enabled boolean NOT NULL,
    allow_sales boolean DEFAULT true NOT NULL,
    allow_purchase boolean DEFAULT true NOT NULL,
    allow_payment boolean DEFAULT true NOT NULL,
    allow_manual_rate boolean DEFAULT false NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT legal_entity_currency_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Disabled'::text])))
);



--
-- Name: payment_term; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.payment_term (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    display_name_ar text NOT NULL,
    display_name_en text NOT NULL,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT payment_term_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Archived'::text])))
);



--
-- Name: payment_term_rule; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.payment_term_rule (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    version_id uuid NOT NULL,
    sequence_no integer NOT NULL,
    allocation_type text NOT NULL,
    percentage numeric(18,8),
    fixed_amount numeric(28,8),
    due_date_basis text NOT NULL,
    days integer DEFAULT 0 NOT NULL,
    day_of_month smallint,
    month_offset integer DEFAULT 0 NOT NULL,
    CONSTRAINT payment_term_rule_allocation_type_check CHECK ((allocation_type = ANY (ARRAY['Percentage'::text, 'Fixed'::text, 'Balance'::text]))),
    CONSTRAINT payment_term_rule_check CHECK ((((allocation_type = 'Percentage'::text) AND (percentage >= (0)::numeric) AND (percentage <= (100)::numeric) AND (fixed_amount IS NULL)) OR ((allocation_type = 'Fixed'::text) AND (fixed_amount IS NOT NULL) AND (percentage IS NULL)) OR ((allocation_type = 'Balance'::text) AND (percentage IS NULL) AND (fixed_amount IS NULL)))),
    CONSTRAINT payment_term_rule_due_date_basis_check CHECK ((due_date_basis = ANY (ARRAY['InvoiceDate'::text, 'DeliveryDate'::text, 'MonthEnd'::text]))),
    CONSTRAINT payment_term_rule_sequence_no_check CHECK ((sequence_no > 0))
);



--
-- Name: payment_term_version; Type: TABLE; Schema: foundation_finbase; Owner: -
--

CREATE TABLE foundation_finbase.payment_term_version (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_term_id uuid NOT NULL,
    version_number integer NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    status text NOT NULL,
    description text,
    CONSTRAINT payment_term_version_check CHECK (((effective_to IS NULL) OR (effective_to >= effective_from))),
    CONSTRAINT payment_term_version_status_check CHECK ((status = ANY (ARRAY['Draft'::text, 'Active'::text, 'Retired'::text]))),
    CONSTRAINT payment_term_version_version_number_check CHECK ((version_number > 0))
);



--
-- Name: currency_reference currency_reference_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.currency_reference
    ADD CONSTRAINT currency_reference_pkey PRIMARY KEY (code);


--
-- Name: exchange_rate exchange_rate_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.exchange_rate
    ADD CONSTRAINT exchange_rate_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: financial_policy financial_policy_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_policy
    ADD CONSTRAINT financial_policy_pkey PRIMARY KEY (tenant_id, legal_entity_id);


--
-- Name: financial_rounding_policy financial_rounding_policy_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_rounding_policy
    ADD CONSTRAINT financial_rounding_policy_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: financial_rounding_policy financial_rounding_policy_tenant_id_code_key; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_rounding_policy
    ADD CONSTRAINT financial_rounding_policy_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: legal_entity_currency legal_entity_currency_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.legal_entity_currency
    ADD CONSTRAINT legal_entity_currency_pkey PRIMARY KEY (tenant_id, legal_entity_id, currency_code);


--
-- Name: payment_term payment_term_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term
    ADD CONSTRAINT payment_term_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: payment_term_rule payment_term_rule_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_rule
    ADD CONSTRAINT payment_term_rule_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: payment_term_rule payment_term_rule_tenant_id_version_id_sequence_no_key; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_rule
    ADD CONSTRAINT payment_term_rule_tenant_id_version_id_sequence_no_key UNIQUE (tenant_id, version_id, sequence_no);


--
-- Name: payment_term payment_term_tenant_id_code_key; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term
    ADD CONSTRAINT payment_term_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: payment_term_version payment_term_version_pkey; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_version
    ADD CONSTRAINT payment_term_version_pkey PRIMARY KEY (tenant_id, id);


--
-- Name: payment_term_version payment_term_version_tenant_id_payment_term_id_version_numb_key; Type: CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_version
    ADD CONSTRAINT payment_term_version_tenant_id_payment_term_id_version_numb_key UNIQUE (tenant_id, payment_term_id, version_number);


--
-- Name: ix_finbase_rate_resolution; Type: INDEX; Schema: foundation_finbase; Owner: -
--

CREATE INDEX ix_finbase_rate_resolution ON foundation_finbase.exchange_rate USING btree (tenant_id, legal_entity_id, from_currency, to_currency, purpose, effective_date DESC, id) WHERE (status = 'Approved'::text);


--
-- Name: exchange_rate exchange_rate_from_currency_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.exchange_rate
    ADD CONSTRAINT exchange_rate_from_currency_fkey FOREIGN KEY (from_currency) REFERENCES foundation_finbase.currency_reference(code);


--
-- Name: exchange_rate exchange_rate_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.exchange_rate
    ADD CONSTRAINT exchange_rate_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: exchange_rate exchange_rate_tenant_id_legal_entity_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.exchange_rate
    ADD CONSTRAINT exchange_rate_tenant_id_legal_entity_id_fkey FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id);


--
-- Name: exchange_rate exchange_rate_to_currency_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.exchange_rate
    ADD CONSTRAINT exchange_rate_to_currency_fkey FOREIGN KEY (to_currency) REFERENCES foundation_finbase.currency_reference(code);


--
-- Name: financial_policy financial_policy_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_policy
    ADD CONSTRAINT financial_policy_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: financial_policy financial_policy_tenant_id_legal_entity_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_policy
    ADD CONSTRAINT financial_policy_tenant_id_legal_entity_id_fkey FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id);


--
-- Name: financial_policy financial_policy_tenant_id_rounding_policy_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_policy
    ADD CONSTRAINT financial_policy_tenant_id_rounding_policy_id_fkey FOREIGN KEY (tenant_id, rounding_policy_id) REFERENCES foundation_finbase.financial_rounding_policy(tenant_id, id);


--
-- Name: financial_rounding_policy financial_rounding_policy_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.financial_rounding_policy
    ADD CONSTRAINT financial_rounding_policy_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: legal_entity_currency legal_entity_currency_currency_code_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.legal_entity_currency
    ADD CONSTRAINT legal_entity_currency_currency_code_fkey FOREIGN KEY (currency_code) REFERENCES foundation_finbase.currency_reference(code);


--
-- Name: legal_entity_currency legal_entity_currency_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.legal_entity_currency
    ADD CONSTRAINT legal_entity_currency_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: legal_entity_currency legal_entity_currency_tenant_id_legal_entity_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.legal_entity_currency
    ADD CONSTRAINT legal_entity_currency_tenant_id_legal_entity_id_fkey FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id);


--
-- Name: payment_term_rule payment_term_rule_tenant_id_version_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_rule
    ADD CONSTRAINT payment_term_rule_tenant_id_version_id_fkey FOREIGN KEY (tenant_id, version_id) REFERENCES foundation_finbase.payment_term_version(tenant_id, id);


--
-- Name: payment_term payment_term_tenant_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term
    ADD CONSTRAINT payment_term_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: payment_term_version payment_term_version_tenant_id_payment_term_id_fkey; Type: FK CONSTRAINT; Schema: foundation_finbase; Owner: -
--

ALTER TABLE ONLY foundation_finbase.payment_term_version
    ADD CONSTRAINT payment_term_version_tenant_id_payment_term_id_fkey FOREIGN KEY (tenant_id, payment_term_id) REFERENCES foundation_finbase.payment_term(tenant_id, id);


--
-- Name: currency_reference; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.currency_reference ENABLE ROW LEVEL SECURITY;

--
-- Name: currency_reference currency_reference_read; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY currency_reference_read ON foundation_finbase.currency_reference FOR SELECT TO erp_app USING (true);


--
-- Name: exchange_rate; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.exchange_rate ENABLE ROW LEVEL SECURITY;

--
-- Name: financial_policy; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.financial_policy ENABLE ROW LEVEL SECURITY;

--
-- Name: financial_rounding_policy; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.financial_rounding_policy ENABLE ROW LEVEL SECURITY;

--
-- Name: legal_entity_currency; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.legal_entity_currency ENABLE ROW LEVEL SECURITY;

--
-- Name: payment_term; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.payment_term ENABLE ROW LEVEL SECURITY;

--
-- Name: payment_term_rule; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.payment_term_rule ENABLE ROW LEVEL SECURITY;

--
-- Name: payment_term_version; Type: ROW SECURITY; Schema: foundation_finbase; Owner: -
--

ALTER TABLE foundation_finbase.payment_term_version ENABLE ROW LEVEL SECURITY;

--
-- Name: exchange_rate tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.exchange_rate USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: financial_policy tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.financial_policy USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: financial_rounding_policy tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.financial_rounding_policy USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: legal_entity_currency tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.legal_entity_currency USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: payment_term tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.payment_term USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: payment_term_rule tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.payment_term_rule USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: payment_term_version tenant_isolation; Type: POLICY; Schema: foundation_finbase; Owner: -
--

CREATE POLICY tenant_isolation ON foundation_finbase.payment_term_version USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: SCHEMA foundation_finbase; Type: ACL; Schema: -; Owner: -
--

GRANT USAGE ON SCHEMA foundation_finbase TO erp_app;


--
-- Name: TABLE currency_reference; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.currency_reference TO erp_app;


--
-- Name: TABLE exchange_rate; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.exchange_rate TO erp_app;


--
-- Name: TABLE financial_policy; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.financial_policy TO erp_app;


--
-- Name: TABLE financial_rounding_policy; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.financial_rounding_policy TO erp_app;


--
-- Name: TABLE legal_entity_currency; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.legal_entity_currency TO erp_app;


--
-- Name: TABLE payment_term; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.payment_term TO erp_app;


--
-- Name: TABLE payment_term_rule; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.payment_term_rule TO erp_app;


--
-- Name: TABLE payment_term_version; Type: ACL; Schema: foundation_finbase; Owner: -
--

GRANT SELECT,INSERT,UPDATE ON TABLE foundation_finbase.payment_term_version TO erp_app;


--
-- PostgreSQL database dump complete
--

