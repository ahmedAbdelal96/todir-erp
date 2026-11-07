import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import type { RegisterResponse } from "../types.ts";

export interface OpenSessionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly registers: readonly RegisterResponse[];
  readonly selectedRegisterId?: string | undefined;
  readonly onOpenSession: (registerId: string, openingFloat: number) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function OpenSessionModal({
  isOpen,
  onClose,
  registers,
  selectedRegisterId,
  onOpenSession,
  isSubmitting = false,
}: OpenSessionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [registerId, setRegisterId] = useState<string>(selectedRegisterId || registers[0]?.id || "");
  const [openingFloatStr, setOpeningFloatStr] = useState<string>("100");
  const [error, setError] = useState<string | null>(null);

  const openingFloat = parseFloat(openingFloatStr) || 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!registerId) {
      setError(t("session.selectRegisterRequired"));
      return;
    }
    if (openingFloat < 0) {
      setError(t("session.invalidFloat"));
      return;
    }
    setError(null);
    try {
      await onOpenSession(registerId, openingFloat);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("session.openFailed"));
    }
  };

  const registerOptions = registers.map((r) => ({
    id: r.id,
    label: `${r.name} (${r.code})`,
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("session.openModalTitle")}
      description={t("session.openModalSubtitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("session.registerLabel")}
          selectedKey={registerId}
          onSelectionChange={(val) => {
            setRegisterId(String(val));
            setError(null);
          }}
          options={registerOptions}
        />

        <TextField
          label={t("session.openingFloatLabel")}
          type="number"
          value={openingFloatStr}
          onChange={(v) => {
            setOpeningFloatStr(v);
            setError(null);
          }}
          autoFocus
        />

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={isSubmitting || !registerId}>
            {isSubmitting ? t("common.submitting") : t("session.openAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface CloseSessionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly expectedCash: number;
  readonly currencyCode?: string | undefined;
  readonly onCloseSession: (countedCash: number, varianceReason?: string | undefined) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function CloseSessionModal({
  isOpen,
  onClose,
  expectedCash,
  currencyCode = "EGP",
  onCloseSession,
  isSubmitting = false,
}: CloseSessionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [countedCashStr, setCountedCashStr] = useState<string>("");
  const [varianceReason, setVarianceReason] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const countedCash = parseFloat(countedCashStr) || 0;
  const variance = Math.round((countedCash - expectedCash) * 100) / 100;
  const hasVariance = variance !== 0 && countedCashStr.trim() !== "";
  const varianceText = `${new Intl.NumberFormat("en-US", { signDisplay: "exceptZero", minimumFractionDigits: 2 }).format(variance)} ${currencyCode}`;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (countedCashStr.trim() === "") {
      setError(t("session.countedCashRequired"));
      return;
    }
    if (hasVariance && !varianceReason.trim()) {
      setError(t("session.varianceReasonRequired"));
      return;
    }
    setError(null);
    try {
      await onCloseSession(countedCash, hasVariance ? varianceReason.trim() : undefined);
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("session.closeFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("session.closeModalTitle")}
      description={t("session.closeModalSubtitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div
          style={{
            padding: "1rem",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-default)",
            background: "var(--color-surface-hover)",
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "1rem",
          }}
        >
          <div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("session.expectedCash")}
            </div>
            <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-default)" }}>
              <BidiText>{expectedCash.toFixed(2)} {currencyCode}</BidiText>
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("session.variance")}
            </div>
            <div
              style={{
                fontSize: "1.25rem",
                fontWeight: 700,
                color:
                  variance === 0
                    ? "var(--color-status-success)"
                    : variance > 0
                    ? "var(--color-brand-primary)"
                    : "var(--color-status-danger)",
              }}
            >
              <BidiText>{varianceText}</BidiText>
            </div>
          </div>
        </div>

        <TextField
          label={`${t("session.countedCashLabel")} (${currencyCode})`}
          type="number"
          placeholder="0.00"
          value={countedCashStr}
          onChange={(v) => {
            setCountedCashStr(v);
            setError(null);
          }}
          autoFocus
        />

        {hasVariance && (
          <TextField
            label={t("session.varianceReasonLabel")}
            placeholder={t("session.varianceReasonPlaceholder")}
            value={varianceReason}
            onChange={(v) => {
              setVarianceReason(v);
              setError(null);
            }}
          />
        )}

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="danger" type="submit" isDisabled={isSubmitting || countedCashStr.trim() === ""}>
            {isSubmitting ? t("common.submitting") : t("session.closeAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
