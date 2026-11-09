import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useCreateCategoryMutation,
  useCreateCategoryVersionMutation,
  useCreatePolicyMutation,
  useCreatePolicyVersionMutation,
  useCreateRatePolicyMutation,
  useImportExpensesMutation
} from "../../hooks/use-expenses-queries.ts";

export interface NewCategoryModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewCategoryModal({ isOpen, onClose, legalEntityId }: NewCategoryModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const createMutation = useCreateCategoryMutation(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [requiresReceipt, setRequiresReceipt] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!code.trim() || !name.trim()) {
      setError(t("modals.newCategory.nameLabel"));
      return;
    }
    try {
      await createMutation.mutateAsync({
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        requiresReceipt
      });
      onClose();
      setCode("");
      setName("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.newCategory.title")} description={t("modals.newCategory.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <TextField label={t("modals.newCategory.codeLabel")} value={code} onChange={(val) => setCode(val)} isRequired />
        <TextField label={t("modals.newCategory.nameLabel")} value={name} onChange={(val) => setName(val)} isRequired />
        <Checkbox label={t("modals.newCategory.requiresReceiptLabel")} isSelected={requiresReceipt} onChange={setRequiresReceipt} />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>{createMutation.isPending ? t("common.loading") : t("modals.newCategory.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewCategoryVersionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly categoryId: string;
}

export function NewCategoryVersionModal({ isOpen, onClose, legalEntityId, categoryId }: NewCategoryVersionModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const createVersionMutation = useCreateCategoryVersionMutation(legalEntityId, categoryId);

  const [requiresReceipt, setRequiresReceipt] = useState(true);
  const [threshold, setThreshold] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await createVersionMutation.mutateAsync({
        requiresReceipt,
        receiptThreshold: threshold ? parseFloat(threshold) : null,
        effectiveFrom,
        effectiveTo: effectiveTo || null
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.newCategoryVersion.title")} description={t("modals.newCategoryVersion.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <Checkbox label={t("modals.newCategoryVersion.requiresReceiptLabel")} isSelected={requiresReceipt} onChange={setRequiresReceipt} />
        <TextField label={t("modals.newCategoryVersion.thresholdLabel")} type="number" value={threshold} onChange={(val) => setThreshold(val)} />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField label={t("modals.newCategoryVersion.effectiveFromLabel")} type="date" value={effectiveFrom} onChange={(val) => setEffectiveFrom(val)} isRequired />
          <TextField label={t("modals.newCategoryVersion.effectiveToLabel")} type="date" value={effectiveTo} onChange={(val) => setEffectiveTo(val)} />
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createVersionMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={createVersionMutation.isPending}>{createVersionMutation.isPending ? t("common.loading") : t("modals.newCategoryVersion.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewPolicyModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewPolicyModal({ isOpen, onClose, legalEntityId }: NewPolicyModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const createMutation = useCreatePolicyMutation(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!code.trim() || !name.trim()) {
      setError(t("modals.newPolicy.nameLabel"));
      return;
    }
    try {
      await createMutation.mutateAsync({ legalEntityId, code: code.trim().toUpperCase(), name: name.trim() });
      onClose();
      setCode("");
      setName("");
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.newPolicy.title")} description={t("modals.newPolicy.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <TextField label={t("modals.newPolicy.codeLabel")} value={code} onChange={(val) => setCode(val)} isRequired />
        <TextField label={t("modals.newPolicy.nameLabel")} value={name} onChange={(val) => setName(val)} isRequired />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>{createMutation.isPending ? t("common.loading") : t("modals.newPolicy.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewPolicyVersionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly policyId: string;
}

export function NewPolicyVersionModal({ isOpen, onClose, legalEntityId, policyId }: NewPolicyVersionModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const createMutation = useCreatePolicyVersionMutation(legalEntityId, policyId);

  const [rules, setRules] = useState('{\n  "maxAmount": 5000,\n  "requireReceiptAbove": 200\n}');
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    let parsedRules: Record<string, unknown>;
    try {
      parsedRules = JSON.parse(rules);
    } catch {
      setError(t("modals.newPolicyVersion.invalidJson"));
      return;
    }
    try {
      await createMutation.mutateAsync({ rules: parsedRules, effectiveFrom, effectiveTo: effectiveTo || null });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.newPolicyVersion.title")} description={t("modals.newPolicyVersion.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <TextField label={t("modals.newPolicyVersion.rulesLabel")} value={rules} onChange={(val) => setRules(val)} isRequired />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField label={t("modals.newPolicyVersion.effectiveFromLabel")} type="date" value={effectiveFrom} onChange={(val) => setEffectiveFrom(val)} isRequired />
          <TextField label={t("modals.newPolicyVersion.effectiveToLabel")} type="date" value={effectiveTo} onChange={(val) => setEffectiveTo(val)} />
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>{createMutation.isPending ? t("common.loading") : t("modals.newPolicyVersion.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewRatePolicyModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewRatePolicyModal({ isOpen, onClose, legalEntityId }: NewRatePolicyModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const createMutation = useCreateRatePolicyMutation(legalEntityId);

  const [kind, setKind] = useState("Mileage");
  const [rate, setRate] = useState("15.00");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [effectiveFrom, setEffectiveFrom] = useState(() => new Date().toISOString().slice(0, 10));
  const [effectiveTo, setEffectiveTo] = useState("");
  const [vehicleType, setVehicleType] = useState("");
  const [locationCode, setLocationCode] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    const parsedRate = parseFloat(rate);
    if (isNaN(parsedRate) || parsedRate <= 0) {
      setError(t("modals.newRatePolicy.rateLabel"));
      return;
    }
    try {
      await createMutation.mutateAsync({
        legalEntityId,
        kind,
        rate: parsedRate,
        currencyCode,
        effectiveFrom,
        effectiveTo: effectiveTo || null,
        vehicleType: kind === "Mileage" ? vehicleType || null : null,
        locationCode: kind === "PerDiem" ? locationCode || null : null
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.newRatePolicy.title")} description={t("modals.newRatePolicy.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <Select
          label={t("modals.newRatePolicy.kindLabel")}
          selectedKey={kind}
          onSelectionChange={(key) => {
            if (key) setKind(String(key));
          }}
          options={[
            { id: "Mileage", label: t("modals.newRatePolicy.kindMileage") },
            { id: "PerDiem", label: t("modals.newRatePolicy.kindPerDiem") }
          ]}
        />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField label={t("modals.newRatePolicy.rateLabel")} type="number" value={rate} onChange={(val) => setRate(val)} isRequired />
          <TextField label={t("modals.newRatePolicy.currencyLabel")} value={currencyCode} onChange={(val) => setCurrencyCode(val.toUpperCase())} isRequired />
        </div>
        {kind === "Mileage" ? (
          <TextField label={t("modals.newRatePolicy.vehicleTypeLabel")} value={vehicleType} onChange={(val) => setVehicleType(val)} />
        ) : (
          <TextField label={t("modals.newRatePolicy.locationCodeLabel")} value={locationCode} onChange={(val) => setLocationCode(val)} />
        )}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField label={t("modals.newRatePolicy.effectiveFromLabel")} type="date" value={effectiveFrom} onChange={(val) => setEffectiveFrom(val)} isRequired />
          <TextField label={t("modals.newRatePolicy.effectiveToLabel")} type="date" value={effectiveTo} onChange={(val) => setEffectiveTo(val)} />
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>{createMutation.isPending ? t("common.loading") : t("modals.newRatePolicy.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface ImportExpensesModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function ImportExpensesModal({ isOpen, onClose, legalEntityId }: ImportExpensesModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const importMutation = useImportExpensesMutation(legalEntityId);

  const [jsonText, setJsonText] = useState("[]");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    let rows: unknown;
    try {
      rows = JSON.parse(jsonText);
      if (!Array.isArray(rows)) {
        setError(t("modals.importExpenses.invalidArray"));
        return;
      }
    } catch {
      setError(t("modals.importExpenses.invalidJson"));
      return;
    }

    try {
      await importMutation.mutateAsync({
        legalEntityId,
        idempotencyKey: `IMP-${Date.now()}`,
        rows: rows as any
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("modals.importExpenses.title")} description={t("modals.importExpenses.description")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && <div role="alert" style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-danger, #fef2f2)", color: "var(--color-status-danger, #dc2626)", borderRadius: "var(--radius-md, 0.5rem)" }}>{error}</div>}
        <TextField label={t("modals.importExpenses.fileLabel")} value={jsonText} onChange={(val) => setJsonText(val)} isRequired />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={importMutation.isPending}>{t("common.cancel")}</Button>
          <Button type="submit" variant="primary" isDisabled={importMutation.isPending}>{importMutation.isPending ? t("common.loading") : t("modals.importExpenses.submitButton")}</Button>
        </div>
      </form>
    </Dialog>
  );
}
