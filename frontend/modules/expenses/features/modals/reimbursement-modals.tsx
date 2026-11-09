import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  usePrepareBatchMutation,
  usePayBatchMutation,
  useSettleRepaymentMutation
} from "../../hooks/use-expenses-queries.ts";
import type { ExpenseEmployeeRepaymentDto } from "../../types.ts";

export interface PrepareBatchModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function PrepareBatchModal({
  isOpen,
  onClose,
  legalEntityId
}: PrepareBatchModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const prepareMutation = usePrepareBatchMutation(legalEntityId);

  const [currency, setCurrency] = useState("EGP");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await prepareMutation.mutateAsync(currency);
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.prepareBatch.title")}
      description={t("modals.prepareBatch.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              backgroundColor: "var(--color-surface-danger, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              borderRadius: "var(--radius-md, 0.5rem)",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("modals.prepareBatch.currencyLabel")}
          value={currency}
          onChange={(val) => setCurrency(val.toUpperCase())}
          isRequired
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={prepareMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={prepareMutation.isPending}>
            {prepareMutation.isPending ? t("common.loading") : t("modals.prepareBatch.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface PayBatchModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly batchId: string;
  readonly version: number;
}

export function PayBatchModal({
  isOpen,
  onClose,
  legalEntityId,
  batchId,
  version
}: PayBatchModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const payMutation = usePayBatchMutation(legalEntityId);

  const [accountId, setAccountId] = useState("00000000-0000-0000-0000-000000000001");
  const [method, setMethod] = useState("BankTransfer");
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [postingDate, setPostingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await payMutation.mutateAsync({
        batchId,
        request: {
          paymentAccountId: accountId,
          method,
          paymentDate,
          postingDate,
          version
        }
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.payBatch.title")}
      description={t("modals.payBatch.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              backgroundColor: "var(--color-surface-danger, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              borderRadius: "var(--radius-md, 0.5rem)",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("modals.payBatch.accountLabel")}
          value={accountId}
          onChange={(val) => setAccountId(val)}
          isRequired
        />

        <Select
          label={t("modals.payBatch.methodLabel")}
          selectedKey={method}
          onSelectionChange={(key) => {
            if (key) setMethod(String(key));
          }}
          options={[
            { id: "BankTransfer", label: "تحويل بنكي (Bank Transfer)" },
            { id: "Cash", label: "صرف نقدي (Cash)" },
            { id: "Cheque", label: "شيك مصرفي (Cheque)" }
          ]}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.payBatch.paymentDateLabel")}
            type="date"
            value={paymentDate}
            onChange={(val) => setPaymentDate(val)}
            isRequired
          />
          <TextField
            label={t("modals.payBatch.postingDateLabel")}
            type="date"
            value={postingDate}
            onChange={(val) => setPostingDate(val)}
            isRequired
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={payMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={payMutation.isPending}>
            {payMutation.isPending ? t("common.loading") : t("modals.payBatch.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface SettleRepaymentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly repayment: ExpenseEmployeeRepaymentDto | null;
}

export function SettleRepaymentModal({
  isOpen,
  onClose,
  legalEntityId,
  repayment
}: SettleRepaymentModalProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const settleMutation = useSettleRepaymentMutation(legalEntityId);

  const [accountId, setAccountId] = useState("00000000-0000-0000-0000-000000000001");
  const [method, setMethod] = useState("BankTransfer");
  const [amount, setAmount] = useState(() => (repayment ? String(repayment.amount - repayment.settledAmount) : ""));
  const [paymentDate, setPaymentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [postingDate, setPostingDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (repayment) {
      setAmount(String(repayment.amount - repayment.settledAmount));
    }
  }, [repayment]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!repayment) return;
    setError(null);

    const parsedAmount = parseFloat(amount);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      setError(t("modals.settleRepayment.amountLabel"));
      return;
    }

    try {
      await settleMutation.mutateAsync({
        repaymentId: repayment.id,
        request: {
          paymentAccountId: accountId,
          method,
          amount: parsedAmount,
          paymentDate,
          postingDate,
          version: repayment.version
        }
      });
      onClose();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : t("common.errorTitle");
      setError(message);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("modals.settleRepayment.title")}
      description={t("modals.settleRepayment.description")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {error && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              backgroundColor: "var(--color-surface-danger, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              borderRadius: "var(--radius-md, 0.5rem)",
              fontSize: "0.875rem"
            }}
          >
            {error}
          </div>
        )}

        <TextField
          label={t("modals.settleRepayment.accountLabel")}
          value={accountId}
          onChange={(val) => setAccountId(val)}
          isRequired
        />

        <Select
          label={t("modals.settleRepayment.methodLabel")}
          selectedKey={method}
          onSelectionChange={(key) => {
            if (key) setMethod(String(key));
          }}
          options={[
            { id: "BankTransfer", label: "إيداع / تحويل بنكي" },
            { id: "Cash", label: "توريد نقدي بالخزينة" },
            { id: "Cheque", label: "شيك مصرفي" }
          ]}
        />

        <TextField
          label={t("modals.settleRepayment.amountLabel")}
          type="number"
          value={amount}
          onChange={(val) => setAmount(val)}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("modals.settleRepayment.paymentDateLabel")}
            type="date"
            value={paymentDate}
            onChange={(val) => setPaymentDate(val)}
            isRequired
          />
          <TextField
            label={t("modals.settleRepayment.postingDateLabel")}
            type="date"
            value={postingDate}
            onChange={(val) => setPostingDate(val)}
            isRequired
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={settleMutation.isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={settleMutation.isPending}>
            {settleMutation.isPending ? t("common.loading") : t("modals.settleRepayment.submitButton")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
