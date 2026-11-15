import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
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
  const [billingModel, setBillingModel] = useState<ProjectBillingModel>("TimeAndMaterials");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [targetEndDate, setTargetEndDate] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [initialBudget, setInitialBudget] = useState("");
  const [description, setDescription] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    createMutation.mutate(
      {
        legalEntityId,
        name: name.trim(),
        projectType,
        billingModel,
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
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
              onChange={(e) => setProjectType(e.target.value as ProjectType)}
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
          </div>
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
              onChange={(e) => setStartDate(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
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

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createMutation.isPending}>
            {t("actions.save")}
          </Button>
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
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createMutation.isPending}>
            {t("actions.save")}
          </Button>
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
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={updateMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
