import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useCreateTimeEntry,
  useCreateBudget,
  useCreateBudgetRevision,
  useCreateForecast,
  useRecordCostFact,
  useRecordCommitment,
  useCreateBillingRule,
  useAddBillingPlanLine,
  useCreateBillableCharge,
  useRequestInvoice,
  useCreateChange,
  useDecideChange,
  useCreateCapitalHandoff,
  useCreateBaseline,
  useAddDocument,
  useAddCloseException,
  useCreateProjectTemplate
} from "../../hooks/use-projects-queries.ts";
import type { ChangeRequestView } from "../../types.ts";

// 1. Log Time Modal
export function LogTimeModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const createMutation = useCreateTimeEntry();

  const [workDate, setWorkDate] = useState(new Date().toISOString().slice(0, 10));
  const [durationHours, setDurationHours] = useState("4");
  const [description, setDescription] = useState("");
  const [billable, setBillable] = useState(true);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const mins = Math.round(parseFloat(durationHours || "0") * 60);
    if (mins <= 0) return;

    try {
      await createMutation.mutateAsync({
        projectId,
        request: {
          projectId,
          employeeId: "83c2eada-5068-1059-8ae4-5ec1dc818ae1",
          workDate,
          durationMinutes: mins,
          description: description.trim() || null,
          billable
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.logTimeTitle")} size="small">
      <form id="log-time-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.date")} *
          </label>
          <input
            type="date"
            required
            value={workDate}
            onChange={(e) => setWorkDate(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.durationHours")} *
          </label>
          <input
            type="number"
            min="0.25"
            step="0.25"
            required
            value={durationHours}
            onChange={(e) => setDurationHours(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <input
            type="text"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="billableTimeCheckbox"
            checked={billable}
            onChange={(e) => setBillable(e.target.checked)}
          />
          <label htmlFor="billableTimeCheckbox" style={{ fontSize: "0.875rem", fontWeight: 600 }}>
            {t("fields.billable")}
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createMutation.isPending}
            data-testid="btn-submit-time"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 2. New Budget Modal
export function NewBudgetModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const createMutation = useCreateBudget();

  const [name, setName] = useState("Original Execution Budget");
  const [amount, setAmount] = useState("");
  const [controlMode, setControlMode] = useState("Warn");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(amount);
    if (isNaN(val) || val < 0) return;

    createMutation.mutate(
      {
        projectId,
        request: {
          name: name.trim(),
          currencyCode: currencyCode || "EGP",
          originalAmount: val,
          controlMode
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newBudgetTitle")} size="small">
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

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} ({currencyCode}) *
          </label>
          <input
            type="number"
            min="0"
            step="any"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.controlMode")}
          </label>
          <select
            value={controlMode}
            onChange={(e) => setControlMode(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
          >
            <option value="Warn">{t("fields.controlModeWarn")}</option>
            <option value="Strict">{t("fields.controlModeStrict")}</option>
          </select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createMutation.isPending} data-testid="btn-submit-time">
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 3. New Budget Revision Modal
export function NewBudgetRevisionModal({
  isOpen,
  onClose,
  projectId,
  budgetId,
  currentAmount
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  budgetId: string;
  currentAmount: number;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const reviseMutation = useCreateBudgetRevision();

  const [revisedAmount, setRevisedAmount] = useState(String(currentAmount));
  const [reason, setReason] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(revisedAmount);
    if (isNaN(val) || val < 0) return;

    reviseMutation.mutate(
      {
        projectId,
        budgetId,
        request: {
          currentAmount: val,
          reason: reason.trim() || null
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newBudgetRevisionTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} *
          </label>
          <input
            type="number"
            min="0"
            step="any"
            required
            value={revisedAmount}
            onChange={(e) => setRevisedAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.reason")}
          </label>
          <input
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={reviseMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 4. New Forecast Modal
export function NewForecastModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const forecastMutation = useCreateForecast();

  const [etcAmount, setEtcAmount] = useState("");
  const [includesOpenCommitments, setIncludesOpenCommitments] = useState(true);
  const [assumptions, setAssumptions] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(etcAmount);
    if (isNaN(val) || val < 0) return;

    try {
      await forecastMutation.mutateAsync({
        projectId,
        request: {
          etcAmount: val,
          includesOpenCommitments,
          assumptions: assumptions.trim() || null
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newForecastTitle")} size="small">
      <form id="new-forecast-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.etcAmount")} *
          </label>
          <input
            type="number"
            min="0"
            step="any"
            required
            value={etcAmount}
            onChange={(e) => setEtcAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="commitmentsForecastCheckbox"
            checked={includesOpenCommitments}
            onChange={(e) => setIncludesOpenCommitments(e.target.checked)}
          />
          <label htmlFor="commitmentsForecastCheckbox" style={{ fontSize: "0.875rem", fontWeight: 600 }}>
            {t("fields.includeCommitments")}
          </label>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.assumptions")}
          </label>
          <input
            type="text"
            value={assumptions}
            onChange={(e) => setAssumptions(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={forecastMutation.isPending}
            data-testid="btn-submit-forecast"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 5. Record Cost Fact Modal
export function RecordCostFactModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const costMutation = useRecordCostFact();

  const [category, setCategory] = useState("Direct");
  const [amount, setAmount] = useState("");
  const [factDate, setFactDate] = useState(new Date().toISOString().slice(0, 10));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(amount);
    if (isNaN(val) || val <= 0) return;

    costMutation.mutate(
      {
        projectId,
        request: {
          projectId,
          sourceModule: "manual",
          sourceType: "DirectCost",
          sourceId: `manual-${Date.now()}`,
          costCategory: category,
          originalCurrency: currencyCode || "EGP",
          originalAmount: val,
          baseCurrency: currencyCode || "EGP",
          baseAmount: val,
          factDate,
          idempotencyKey: `cost-fact-${Date.now()}`
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.recordCostFactTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.category")} *
          </label>
          <input
            type="text"
            required
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} ({currencyCode}) *
          </label>
          <input
            type="number"
            min="0.01"
            step="any"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.date")} *
          </label>
          <input
            type="date"
            required
            value={factDate}
            onChange={(e) => setFactDate(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={costMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 6. Record PO Commitment Modal
export function RecordCommitmentModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const commitMutation = useRecordCommitment();

  const [poNumber, setPoNumber] = useState("");
  const [amount, setAmount] = useState("");
  const [commitDate, setCommitDate] = useState(new Date().toISOString().slice(0, 10));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(amount);
    if (isNaN(val) || val <= 0 || !poNumber.trim()) return;

    commitMutation.mutate(
      {
        projectId,
        request: {
          projectId,
          sourceModule: "purchasing",
          sourceId: poNumber.trim(),
          commitmentDate: commitDate,
          currencyCode: currencyCode || "EGP",
          committedAmount: val,
          actualizedAmount: 0,
          idempotencyKey: `po-commit-${Date.now()}`
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.recordCommitmentTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.poReference")}
          </label>
          <input
            type="text"
            required
            value={poNumber}
            onChange={(e) => setPoNumber(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} ({currencyCode}) *
          </label>
          <input
            type="number"
            min="0.01"
            step="any"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.date")} *
          </label>
          <input
            type="date"
            required
            value={commitDate}
            onChange={(e) => setCommitDate(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={commitMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 7. New Billing Rule Modal
export function NewBillingRuleModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const ruleMutation = useCreateBillingRule();

  const [model, setModel] = useState("TimeAndMaterials");
  const [contractValue, setContractValue] = useState("");
  const [markupPercent, setMarkupPercent] = useState("0");
  const [billingCap, setBillingCap] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    ruleMutation.mutate(
      {
        projectId,
        request: {
          model,
          currencyCode: currencyCode || "EGP",
          contractValue: contractValue ? parseFloat(contractValue) : null,
          markupPercent: parseFloat(markupPercent || "0"),
          billingCap: billingCap ? parseFloat(billingCap) : null
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newBillingRuleTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.ruleModel")} *
          </label>
          <select
            value={model}
            onChange={(e) => setModel(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
          >
            <option value="TimeAndMaterials">{t("billingModels.TimeAndMaterials")}</option>
            <option value="FixedPrice">{t("billingModels.FixedPrice")}</option>
            <option value="Milestone">{t("billingModels.Milestone")}</option>
            <option value="CostPlus">{t("billingModels.CostPlus")}</option>
          </select>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.contractValue")}
          </label>
          <input
            type="number"
            min="0"
            step="any"
            value={contractValue}
            onChange={(e) => setContractValue(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.markupPercent")}
            </label>
            <input
              type="number"
              min="0"
              step="any"
              value={markupPercent}
              onChange={(e) => setMarkupPercent(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.billingCap")}
            </label>
            <input
              type="number"
              min="0"
              step="any"
              value={billingCap}
              onChange={(e) => setBillingCap(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={ruleMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 8. New Billing Plan Line Modal
export function NewBillingPlanLineModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const planMutation = useAddBillingPlanLine();

  const [amount, setAmount] = useState("");
  const [dueDate, setDueDate] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(amount);
    if (isNaN(val) || val <= 0) return;

    planMutation.mutate(
      {
        projectId,
        request: {
          billingRuleId: "00000000-0000-0000-0000-000000000000",
          dueDate: dueDate || null,
          amount: val
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newBillingPlanLineTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} *
          </label>
          <input
            type="number"
            min="0.01"
            step="any"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.dueDate")}
          </label>
          <input
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={planMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 9. Create Manual Billable Charge Modal
export function CreateChargeModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const chargeMutation = useCreateBillableCharge();

  const [description, setDescription] = useState("");
  const [costAmount, setCostAmount] = useState("");
  const [billableAmount, setBillableAmount] = useState("");
  const [chargeDate, setChargeDate] = useState(new Date().toISOString().slice(0, 10));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const cost = parseFloat(costAmount || "0");
    const billable = parseFloat(billableAmount);
    if (isNaN(billable) || billable <= 0) return;

    try {
      await chargeMutation.mutateAsync({
        projectId,
        request: {
          projectId,
          sourceType: "ManualCharge",
          sourceId: `charge-${Date.now()}`,
          chargeDate,
          description: description.trim() || t("dialogs.createChargeTitle"),
          currencyCode: currencyCode || "EGP",
          costAmount: cost,
          billableAmount: billable,
          idempotencyKey: `charge-${Date.now()}`
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.createChargeTitle")} size="small">
      <form id="create-charge-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")} *
          </label>
          <input
            type="text"
            required
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.costAmount")}
            </label>
            <input
              type="number"
              min="0"
              step="any"
              value={costAmount}
              onChange={(e) => setCostAmount(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.billingAmount")} *
            </label>
            <input
              type="number"
              min="0.01"
              step="any"
              required
              value={billableAmount}
              onChange={(e) => setBillableAmount(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={chargeMutation.isPending}
            data-testid="btn-submit-charge"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 10. Request Invoice Modal
export function RequestInvoiceModal({
  isOpen,
  onClose,
  projectId,
  eligibleAmount,
  currencyCode = "EGP",
  defaultChargeId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  eligibleAmount: number;
  currencyCode?: string | undefined;
  defaultChargeId?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const invoiceMutation = useRequestInvoice();

  const [requestedAmount, setRequestedAmount] = useState(String(eligibleAmount || 0));
  const [chargeId, setChargeId] = useState(defaultChargeId || "");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(requestedAmount);
    if (isNaN(val) || val <= 0) return;

    const chargeIds = chargeId.trim() ? [chargeId.trim()] : [];

    try {
      await invoiceMutation.mutateAsync({
        projectId,
        request: {
          projectId,
          chargeIds,
          currencyCode: currencyCode || "EGP",
          requestedAmount: val,
          idempotencyKey: `inv-req-${Date.now()}`
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.requestInvoiceTitle")} size="small">
      <form id="request-invoice-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.chargeId")}
          </label>
          <input
            type="text"
            value={chargeId}
            onChange={(e) => setChargeId(e.target.value)}
            data-testid="input-charge-id"
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.requestedAmount")} ({currencyCode}) *
          </label>
          <input
            type="number"
            min="0.01"
            step="any"
            required
            value={requestedAmount}
            onChange={(e) => setRequestedAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
          {t("kpis.billingEligible")}: {eligibleAmount} {currencyCode}
        </p>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={invoiceMutation.isPending}
            data-testid="btn-submit-invoice-req"
          >
            {t("actions.requestInvoice")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 11. New Change Request Modal
export function NewChangeRequestModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const changeMutation = useCreateChange();

  const [title, setTitle] = useState("");
  const [reason, setReason] = useState("");
  const [contractDelta, setContractDelta] = useState("0");
  const [budgetDelta, setBudgetDelta] = useState("0");
  const [scheduleDeltaDays, setScheduleDeltaDays] = useState("0");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;

    try {
      await changeMutation.mutateAsync({
        projectId,
        request: {
          title: title.trim(),
          reason: reason.trim() || null,
          contractDelta: parseFloat(contractDelta || "0"),
          budgetDelta: parseFloat(budgetDelta || "0"),
          scheduleDeltaDays: parseInt(scheduleDeltaDays || "0", 10)
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newChangeRequestTitle")} size="small">
      <form id="new-change-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.title")} *
          </label>
          <input
            type="text"
            required
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.reason")}
          </label>
          <input
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.contractDelta")}
            </label>
            <input
              type="number"
              step="any"
              value={contractDelta}
              onChange={(e) => setContractDelta(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.budgetDelta")}
            </label>
            <input
              type="number"
              step="any"
              value={budgetDelta}
              onChange={(e) => setBudgetDelta(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.scheduleDeltaDays")}
          </label>
          <input
            type="number"
            value={scheduleDeltaDays}
            onChange={(e) => setScheduleDeltaDays(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={changeMutation.isPending}
            data-testid="btn-submit-change"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 12. Decide Change Modal
export function DecideChangeModal({
  isOpen,
  onClose,
  projectId,
  change
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  change: ChangeRequestView | null;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const decideMutation = useDecideChange();

  const [reason, setReason] = useState("");

  const handleDecision = async (decision: "approve" | "reject") => {
    if (!change) return;

    try {
      await decideMutation.mutateAsync({
        projectId,
        changeId: change.id,
        request: {
          decision,
          reason: reason.trim() || null
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.decideChangeTitle")} size="small">
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {change && (
          <div>
            <strong>{change.requestNumber}:</strong> {change.title}
          </div>
        )}

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.reason")}
          </label>
          <input
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="secondary"
            type="button"
            isDisabled={decideMutation.isPending}
            onClick={() => handleDecision("reject")}
            data-testid="btn-decide-reject"
          >
            {t("actions.reject")}
          </Button>
          <Button
            variant="primary"
            type="button"
            isDisabled={decideMutation.isPending}
            onClick={() => handleDecision("approve")}
            data-testid="btn-decide-approve"
          >
            {t("actions.approve")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}

// 13. Capital Handoff Modal
export function CapitalHandoffModal({
  isOpen,
  onClose,
  projectId,
  currencyCode = "EGP"
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
  currencyCode?: string | undefined;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const handoffMutation = useCreateCapitalHandoff();

  const [costFactId, setCostFactId] = useState("");
  const [amount, setAmount] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const val = parseFloat(amount);
    if (isNaN(val) || val <= 0 || !costFactId.trim()) return;

    try {
      await handoffMutation.mutateAsync({
        projectId,
        request: {
          costFactId: costFactId.trim(),
          amount: val,
          currencyCode: currencyCode || "EGP",
          idempotencyKey: `handoff-${Date.now()}`
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.capitalHandoffTitle")} size="small">
      <form id="capital-handoff-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.costFactId")}
          </label>
          <input
            type="text"
            required
            value={costFactId}
            onChange={(e) => setCostFactId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.handoffAmount")} ({currencyCode}) *
          </label>
          <input
            type="number"
            min="0.01"
            step="any"
            required
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={handoffMutation.isPending}
            data-testid="btn-submit-capital-handoff"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 14. Create Baseline Modal
export function CreateBaselineModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const baselineMutation = useCreateBaseline();

  const [baselineType, setBaselineType] = useState("Scope");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    baselineMutation.mutate(
      {
        projectId,
        request: {
          baselineType,
          snapshotJson: "{}"
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.createBaselineTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.baselineType")}
          </label>
          <select
            value={baselineType}
            onChange={(e) => setBaselineType(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
          >
            <option value="Scope">{t("fields.baselineScope")}</option>
            <option value="Schedule">{t("fields.baselineSchedule")}</option>
            <option value="Cost">{t("fields.baselineCost")}</option>
          </select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={baselineMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 15. Add Document Modal
export function AddDocumentModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const docMutation = useAddDocument();

  const [documentRef, setDocumentRef] = useState("");
  const [documentType, setDocumentType] = useState("Contract");
  const [title, setTitle] = useState("");
  const [sensitivity, setSensitivity] = useState("Normal");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!documentRef.trim()) return;

    docMutation.mutate(
      {
        projectId,
        request: {
          documentRef: documentRef.trim(),
          documentType,
          title: title.trim() || null,
          sensitivity
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
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.addDocumentTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.title")}
          </label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.documentRef")} *
          </label>
          <input
            type="text"
            required
            value={documentRef}
            onChange={(e) => setDocumentRef(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.documentType")}
            </label>
            <select
              value={documentType}
              onChange={(e) => setDocumentType(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
            >
              <option value="Contract">{t("fields.docContract")}</option>
              <option value="Specification">{t("fields.docSpecification")}</option>
              <option value="AcceptancePack">{t("fields.docAcceptancePack")}</option>
              <option value="Other">{t("fields.docOther")}</option>
            </select>
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.sensitivity")}
            </label>
            <select
              value={sensitivity}
              onChange={(e) => setSensitivity(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
            >
              <option value="Normal">{t("fields.securityNormal")}</option>
              <option value="Confidential">{t("fields.securityConfidential")}</option>
            </select>
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={docMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 16. Add Close Exception Modal
export function AddCloseExceptionModal({
  isOpen,
  onClose,
  projectId
}: {
  isOpen: boolean;
  onClose: () => void;
  projectId: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const exceptionMutation = useAddCloseException();

  const [exceptionType, setExceptionType] = useState("Operational");
  const [referenceId, setReferenceId] = useState("");
  const [message, setMessage] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!message.trim()) return;

    try {
      await exceptionMutation.mutateAsync({
        projectId,
        request: {
          exceptionType,
          referenceId: referenceId.trim() || null,
          message: message.trim()
        }
      });
      onClose();
    } catch {
      // Handled
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.addCloseExceptionTitle")} size="small">
      <form id="add-close-exception-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.exceptionType")} *
          </label>
          <input
            type="text"
            required
            value={exceptionType}
            onChange={(e) => setExceptionType(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.sourceRefId")}
          </label>
          <input
            type="text"
            value={referenceId}
            onChange={(e) => setReferenceId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.exceptionMessage")} *
          </label>
          <textarea
            rows={3}
            required
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={exceptionMutation.isPending}
            data-testid="btn-submit-close-exception"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// 17. New Template Modal
export function NewTemplateModal({
  isOpen,
  onClose,
  legalEntityId
}: {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId?: string;
}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const templateMutation = useCreateProjectTemplate();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [projectType, setProjectType] = useState("CustomerProject");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    templateMutation.mutate(
      {
        legalEntityId: legalEntityId || null,
        code: code.trim(),
        name: name.trim(),
        projectType,
        definitionJson: "{}"
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.createTemplateTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.code")} *
          </label>
          <input
            type="text"
            required
            value={code}
            onChange={(e) => setCode(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

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

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.projectType")}
          </label>
          <select
            value={projectType}
            onChange={(e) => setProjectType(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
          >
            <option value="CustomerProject">{t("types.CustomerProject")}</option>
            <option value="InternalProject">{t("types.InternalProject")}</option>
            <option value="CapitalProject">{t("types.CapitalProject")}</option>
          </select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={templateMutation.isPending}>
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
