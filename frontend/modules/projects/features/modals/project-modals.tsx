import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { PartyPicker } from "../../../directory/public.ts";
import { HrEmployeePicker } from "../../../hr/public.ts";
import { useCreateProject, useCreateProjectFromSalesSource, useUpdateProject } from "../../hooks/use-projects-queries.ts";
import type { ProjectDetail, ProjectType, ProjectBillingModel, ProjectExecutionHealth } from "../../types.ts";

export interface NewProjectModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewProjectModal({ isOpen, onClose, legalEntityId }: NewProjectModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const createMutation = useCreateProject();

  const [name, setName] = useState("");
  const [projectType, setProjectType] = useState<ProjectType>("CustomerProject");
  const [customerPartyId, setCustomerPartyId] = useState<string | null>(null);
  const [projectManagerEmployeeId, setProjectManagerEmployeeId] = useState<string | null>(null);
  const [billingModel, setBillingModel] = useState<ProjectBillingModel>("TimeAndMaterials");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [targetEndDate, setTargetEndDate] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [initialBudget, setInitialBudget] = useState("");
  const [description, setDescription] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setName("");
      setProjectType("CustomerProject");
      setCustomerPartyId(null);
      setProjectManagerEmployeeId(null);
      setBillingModel("TimeAndMaterials");
      setStartDate(new Date().toISOString().slice(0, 10));
      setTargetEndDate("");
      setCurrencyCode("EGP");
      setInitialBudget("");
      setDescription("");
      setValidationError(null);
    }
  }, [isOpen]);

  const handleProjectTypeChange = (newType: ProjectType) => {
    setProjectType(newType);
    setValidationError(null);
    if (newType === "CapitalProject") {
      setBillingModel("NonBillable");
      setCustomerPartyId(null);
    } else if (newType === "InternalProject") {
      setCustomerPartyId(null);
      if (billingModel !== "TimeAndMaterials" && billingModel !== "NonBillable") {
        setBillingModel("NonBillable");
      }
    } else if (newType === "CustomerProject") {
      if (billingModel === "NonBillable") {
        setBillingModel("TimeAndMaterials");
      }
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError(null);
    if (!name.trim()) return;

    if (projectType === "CustomerProject" && !customerPartyId) {
      setValidationError(t("errors.customerRequired"));
      return;
    }

    if (targetEndDate && targetEndDate < startDate) {
      setValidationError(t("errors.dateRangeInvalid"));
      return;
    }

    createMutation.mutate(
      {
        legalEntityId,
        name: name.trim(),
        projectType,
        customerPartyId: projectType === "CustomerProject" ? customerPartyId : null,
        projectManagerEmployeeId: projectManagerEmployeeId || null,
        billingModel: projectType === "CapitalProject" ? "NonBillable" : billingModel,
        startDate,
        targetEndDate: targetEndDate ? targetEndDate : null,
        currencyCode: currencyCode ? currencyCode.toUpperCase() : "EGP",
        initialBudget: initialBudget ? parseFloat(initialBudget) : null,
        description: description.trim() ? description.trim() : null
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("dialogs.newProjectTitle")}
      size="medium"
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", width: "100%" }}>
          <Button variant="secondary" onClick={onClose} type="button" data-testid="btn-cancel-project">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="button"
            onClick={() => (document.getElementById("new-project-form") as HTMLFormElement)?.requestSubmit()}
            isDisabled={createMutation.isPending}
            data-testid="btn-save-project"
          >
            {t("actions.save")}
          </Button>
        </div>
      }
    >
      <form id="new-project-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {(validationError || createMutation.isError) && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "6px",
              background: "#fef2f2",
              border: "1px solid #fca5a5",
              color: "#991b1b",
              fontSize: "0.875rem",
              fontWeight: 600
            }}
          >
            {validationError || t("messages.operationFailed")}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.name")} *
          </label>
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.projectType")}
            </label>
            <select
              value={projectType}
              onChange={(e) => handleProjectTypeChange(e.target.value as ProjectType)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
            >
              <option value="CustomerProject">{t("types.CustomerProject")}</option>
              <option value="InternalProject">{t("types.InternalProject")}</option>
              <option value="CapitalProject">{t("types.CapitalProject")}</option>
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.billingModel")}
            </label>
            {projectType === "CapitalProject" ? (
              <div>
                <select
                  disabled
                  value="NonBillable"
                  style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#f1f5f9", color: "#64748b", boxSizing: "border-box" }}
                >
                  <option value="NonBillable">{t("billingModels.NonBillable")}</option>
                </select>
                <span style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.25rem", display: "block" }}>
                  {t("help.capitalBillingNotice")}
                </span>
              </div>
            ) : (
              <select
                value={billingModel}
                onChange={(e) => setBillingModel(e.target.value as ProjectBillingModel)}
                style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
              >
                <option value="TimeAndMaterials">{t("billingModels.TimeAndMaterials")}</option>
                <option value="FixedPrice">{t("billingModels.FixedPrice")}</option>
                <option value="Milestone">{t("billingModels.Milestone")}</option>
                <option value="CostPlus">{t("billingModels.CostPlus")}</option>
                <option value="NonBillable">{t("billingModels.NonBillable")}</option>
              </select>
            )}
          </div>
        </div>

        {projectType === "CustomerProject" && (
          <div>
            <PartyPicker
              label={t("fields.customer")}
              value={customerPartyId ?? undefined}
              onChange={(partyId) => {
                setCustomerPartyId(partyId || null);
                setValidationError(null);
              }}
              roleFilter="Customer"
              isRequired
              enableQuickCreate={true}
            />
          </div>
        )}

        <div>
          <HrEmployeePicker
            label={t("fields.manager")}
            value={projectManagerEmployeeId}
            onChange={(empId) => setProjectManagerEmployeeId(empId || null)}
            legalEntityId={legalEntityId}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.startDate")} *
            </label>
            <input
              type="date"
              required
              value={startDate}
              onChange={(e) => {
                setStartDate(e.target.value);
                setValidationError(null);
              }}
              dir="ltr"
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box", textAlign: "start" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.targetEndDate")}
            </label>
            <input
              type="date"
              value={targetEndDate}
              onChange={(e) => {
                setTargetEndDate(e.target.value);
                setValidationError(null);
              }}
              dir="ltr"
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box", textAlign: "start" }}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.initialBudget")}
            </label>
            <input
              type="number"
              min="0"
              step="any"
              value={initialBudget}
              onChange={(e) => setInitialBudget(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.currencyCode")}
            </label>
            <input
              type="text"
              maxLength={3}
              value={currencyCode}
              onChange={(e) => setCurrencyCode(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <textarea
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>
      </form>
    </Dialog>
  );
}

export interface FromSalesSourceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function FromSalesSourceModal({ isOpen, onClose, legalEntityId }: FromSalesSourceModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const createMutation = useCreateProjectFromSalesSource();

  const [name, setName] = useState("");
  const [quotationId, setQuotationId] = useState("");
  const [acceptedRevision, setAcceptedRevision] = useState(1);
  const [projectManagerEmployeeId, setProjectManagerEmployeeId] = useState<string | null>(null);
  const [billingModel, setBillingModel] = useState<ProjectBillingModel>("FixedPrice");
  const [initialBudget, setInitialBudget] = useState("");
  const [description, setDescription] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !quotationId.trim()) return;

    createMutation.mutate(
      {
        legalEntityId,
        quotationId: quotationId.trim(),
        acceptedRevision: Number(acceptedRevision),
        name: name.trim(),
        projectManagerEmployeeId: projectManagerEmployeeId || null,
        billingModel,
        initialBudget: initialBudget ? parseFloat(initialBudget) : null,
        description: description.trim() ? description.trim() : null,
        idempotencyKey: `prj-sales-${Date.now()}`
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("dialogs.fromSalesSourceTitle")}
      size="medium"
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", width: "100%" }}>
          <Button variant="secondary" onClick={onClose} type="button" data-testid="btn-cancel-from-sales">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="from-sales-form"
            onClick={() => (document.getElementById("from-sales-form") as HTMLFormElement)?.requestSubmit()}
            isDisabled={createMutation.isPending}
            data-testid="btn-save-from-sales"
          >
            {t("actions.save")}
          </Button>
        </div>
      }
    >
      <form id="from-sales-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.name")} *
          </label>
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.quotationId")} *
            </label>
            <input
              type="text"
              required
              value={quotationId}
              onChange={(e) => setQuotationId(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.acceptedRevision")} *
            </label>
            <input
              type="number"
              min="1"
              required
              value={acceptedRevision}
              onChange={(e) => setAcceptedRevision(parseInt(e.target.value, 10))}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <HrEmployeePicker
            label={t("fields.manager")}
            value={projectManagerEmployeeId}
            onChange={(empId) => setProjectManagerEmployeeId(empId || null)}
            legalEntityId={legalEntityId}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.billingModel")}
            </label>
            <select
              value={billingModel}
              onChange={(e) => setBillingModel(e.target.value as ProjectBillingModel)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
            >
              <option value="FixedPrice">{t("billingModels.FixedPrice")}</option>
              <option value="TimeAndMaterials">{t("billingModels.TimeAndMaterials")}</option>
              <option value="Milestone">{t("billingModels.Milestone")}</option>
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.initialBudget")}
            </label>
            <input
              type="number"
              min="0"
              step="any"
              value={initialBudget}
              onChange={(e) => setInitialBudget(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <textarea
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>
      </form>
    </Dialog>
  );
}

export interface EditProjectModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly project: ProjectDetail;
}

export function EditProjectModal({ isOpen, onClose, project }: EditProjectModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const updateMutation = useUpdateProject();

  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description || "");
  const [targetEndDate, setTargetEndDate] = useState(project.targetEndDate || "");
  const [executionHealth, setExecutionHealth] = useState<ProjectExecutionHealth>(project.executionHealth);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    updateMutation.mutate(
      {
        projectId: project.id,
        request: {
          name: name.trim(),
          description: description.trim() ? description.trim() : null,
          targetEndDate: targetEndDate ? targetEndDate : null,
          executionHealth,
          version: project.version
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("dialogs.editProjectTitle")}
      size="medium"
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", width: "100%" }}>
          <Button variant="secondary" onClick={onClose} type="button" data-testid="btn-cancel-edit-project">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="edit-project-form"
            onClick={() => (document.getElementById("edit-project-form") as HTMLFormElement)?.requestSubmit()}
            isDisabled={updateMutation.isPending}
            data-testid="btn-save-edit-project"
          >
            {t("actions.save")}
          </Button>
        </div>
      }
    >
      <form id="edit-project-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.name")} *
          </label>
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.executionHealth")}
            </label>
            <select
              value={executionHealth}
              onChange={(e) => setExecutionHealth(e.target.value as ProjectExecutionHealth)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
            >
              <option value="OnTrack">{t("health.OnTrack")}</option>
              <option value="AtRisk">{t("health.AtRisk")}</option>
              <option value="OffTrack">{t("health.OffTrack")}</option>
              <option value="NotAssessed">{t("health.NotAssessed")}</option>
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.targetEndDate")}
            </label>
            <input
              type="date"
              value={targetEndDate}
              onChange={(e) => setTargetEndDate(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <textarea
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>
      </form>
    </Dialog>
  );
}
