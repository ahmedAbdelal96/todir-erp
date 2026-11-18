import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useResolveCashDifference } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import type { CashCountDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly cashCount: CashCountDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ResolveDifferenceModal({ legalEntityId, accountId, cashCount, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const resolveMutation = useResolveCashDifference(legalEntityId, accountId);

  const [reasonCode, setReasonCode] = useState("ApprovedShortage");
  const [resolutionType, setResolutionType] = useState("ExpenseAdjustment");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen || !cashCount) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    try {
      await resolveMutation.mutateAsync({
        cashCountId: cashCount.id,
        request: {
          reasonCode: reasonCode.trim(),
          resolutionType: resolutionType.trim(),
          amount: Math.abs(cashCount.difference),
          evidenceReference: evidenceReference.trim() || null,
          version: cashCount.version
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("cash.diffModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "460px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div>
          <Select
            label={t("cash.reasonCodeLabel")}
            selectedKey={reasonCode}
            onSelectionChange={(key) => setReasonCode(key ? String(key) : "ApprovedShortage")}
            options={[
              { id: "ApprovedShortage", label: "عجز نقدي معتمد (Approved Shortage)" },
              { id: "ApprovedOverage", label: "زيادة نقدية معتمدة (Approved Overage)" },
              { id: "CashierVariance", label: "فارق تسليم كاشير (Cashier Variance)" }
            ]}
          />
        </div>

        <div>
          <Select
            label={t("cash.resolutionTypeLabel")}
            selectedKey={resolutionType}
            onSelectionChange={(key) => setResolutionType(key ? String(key) : "ExpenseAdjustment")}
            options={[
              { id: "ExpenseAdjustment", label: "تسوية مصروفات خزينة (Expense Adjustment)" },
              { id: "CustodianCharge", label: "تحميل على أمين العهدة (Custodian Charge)" }
            ]}
          />
        </div>

        <TextField
          label={t("cash.amountLabel")}
          type="number"
          value={String(Math.abs(cashCount.difference))}
          isReadOnly
        />

        <TextField
          label={t("cash.evidenceLabel")}
          value={evidenceReference}
          onChange={(val) => setEvidenceReference(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={resolveMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={resolveMutation.isPending}>
            {resolveMutation.isPending ? t("common.loading") : t("cash.diffSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
