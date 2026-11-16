import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useCreateContractMutation } from "../../hooks/use-service-queries.ts";

export interface NewContractModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewContractModal({
  isOpen,
  onClose,
  legalEntityId
}: NewContractModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const createMutation = useCreateContractMutation(legalEntityId);

  const now = new Date();
  const defaultStart = now.toISOString().slice(0, 10);
  const defaultEnd = new Date(now.getTime() + 365 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);

  const [contractNumber, setContractNumber] = useState(`SC-${Date.now().toString().slice(-6)}`);
  const [customerName, setCustomerName] = useState("");
  const [startDate, setStartDate] = useState(defaultStart);
  const [endDate, setEndDate] = useState(defaultEnd);
  const [slaProfileId, setSlaProfileId] = useState("");
  const [coverageTerms, setCoverageTerms] = useState("Full parts & labor coverage 24/7");
  const [price, setPrice] = useState("12000");
  const [currency, setCurrency] = useState("SAR");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!contractNumber.trim() || !customerName.trim()) {
      setErrorMsg(t("validation.contractAndCustomerRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await (createMutation.mutateAsync as any)({
        legalEntityId,
        contractNumber: contractNumber.trim(),
        customerName: customerName.trim(),
        startDateUtc: new Date(startDate).toISOString(),
        endDateUtc: new Date(endDate).toISOString(),
        slaProfileId: slaProfileId.trim() || undefined,
        coverageTerms: coverageTerms.trim() || undefined,
        price: Number(price) || 0,
        currency: currency.trim() || "SAR"
      });
      onClose();
      // Reset form
      setCustomerName("");
      setSlaProfileId("");
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("contracts.newContractTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("contracts.contractNumber")} *
            </label>
            <TextField
              value={contractNumber}
              onChange={(val) => setContractNumber(val)}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.customerName")} *
            </label>
            <TextField
              value={customerName}
              onChange={(val) => setCustomerName(val)}
              placeholder={t("requests.customerPlaceholder")}
              isRequired
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("contracts.startDate")} *
            </label>
            <input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "#ffffff",
                fontSize: "0.875rem",
                boxSizing: "border-box"
              }}
              required
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("contracts.endDate")} *
            </label>
            <input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "#ffffff",
                fontSize: "0.875rem",
                boxSizing: "border-box"
              }}
              required
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("contracts.contractPrice")}
            </label>
            <TextField
              value={price}
              onChange={(val) => setPrice(val)}
              placeholder="12000"
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("billing.currency")}
            </label>
            <TextField
              value={currency}
              onChange={(val) => setCurrency(val)}
              placeholder="SAR"
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("contracts.coverageTerms")}
          </label>
          <TextArea
            value={coverageTerms}
            onChange={(val: any) => setCoverageTerms(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("contracts.coverageTermsPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={createMutation.isPending}>
            {t("contracts.createContract")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
