import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { CashIcon, CreditCardIcon, SplitIcon } from "./pos-icons.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import {
  toScaledBigInt,
  fromScaledBigInt,
  scaledToNumber,
  calculateExactChange,
  calculateExactSplitRemaining,
} from "../utils/exact-money-math.ts";
import type { PosPaymentAccount, PosTenderMethod, TenderRequest } from "../types.ts";

export interface TenderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly totalAmount: number;
  readonly currencyCode?: string | undefined;
  readonly paymentAccounts: readonly PosPaymentAccount[];
  readonly providerConfigId?: string | null | undefined;
  readonly onCheckout: (tenders: readonly TenderRequest[]) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function TenderModal({
  isOpen,
  onClose,
  totalAmount,
  currencyCode = "EGP",
  paymentAccounts,
  providerConfigId,
  onCheckout,
  isSubmitting = false,
}: TenderModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [activeTab, setActiveTab] = useState<"cash" | "card" | "split">("cash");

  // Dynamically resolve the cashbox payment account
  const defaultCashAccount =
    paymentAccounts.find(
      (a) =>
        (a.type?.toLowerCase() === "cashbox" || a.type === "Cashbox") &&
        (a.status === "Active" || !a.status)
    ) ||
    paymentAccounts.find((a) => a.status === "Active") ||
    paymentAccounts[0];

  // Cash state
  const [cashTenderedStr, setCashTenderedStr] = useState<string>(
    totalAmount > 0 ? totalAmount.toFixed(2) : "0"
  );

  useEffect(() => {
    if (isOpen) {
      setCashTenderedStr(totalAmount > 0 ? totalAmount.toFixed(2) : "0");
      setError(null);
    }
  }, [isOpen, totalAmount]);

  // Split state
  const [splitTenders, setSplitTenders] = useState<Array<{ method: PosTenderMethod; amount: number; amountStr: string }>>([]);
  const [splitCurrentMethod, setSplitCurrentMethod] = useState<PosTenderMethod>("Cash");
  const [splitAmountStr, setSplitAmountStr] = useState<string>("");

  const [error, setError] = useState<string | null>(null);

  // Exact math using scaled BigInt arithmetic (zero IEEE 754 float drift)
  const { changeAmount, changeStr, isSufficient } = calculateExactChange(cashTenderedStr, totalAmount);
  const { paidTotal, paidTotalStr, remainingAmount, remainingStr, isComplete } = calculateExactSplitRemaining(
    totalAmount,
    splitTenders
  );

  const handleQuickCash = (amount: number) => {
    setCashTenderedStr(amount.toFixed(2));
    setError(null);
  };

  const handleAddSplitTender = () => {
    const amtScaled = toScaledBigInt(splitAmountStr);
    if (amtScaled <= 0n) return;
    setSplitTenders([
      ...splitTenders,
      {
        method: splitCurrentMethod,
        amount: scaledToNumber(amtScaled),
        amountStr: fromScaledBigInt(amtScaled, 2),
      },
    ]);
    setSplitAmountStr("");
  };

  const handleRemoveSplitTender = (idx: number) => {
    setSplitTenders(splitTenders.filter((_, i) => i !== idx));
  };

  const handleCashSubmit = async () => {
    if (!defaultCashAccount?.id) {
      setError(t("tender.noCashAccountAvailable", "No active cash account is configured for this legal entity."));
      return;
    }
    if (!isSufficient) {
      setError(t("tender.insufficientCash"));
      return;
    }
    setError(null);
    try {
      const tenders: TenderRequest[] = [
        {
          method: "Cash",
          amount: totalAmount,
          paymentAccountId: defaultCashAccount.id,
        },
      ];
      await onCheckout(tenders);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("tender.checkoutFailed"));
    }
  };

  const handleCardSubmit = async () => {
    if (!providerConfigId) {
      setError(t("tender.noCardProviderAvailable", "Card payment provider is not configured for this legal entity."));
      return;
    }
    setError(null);
    try {
      const tenders: TenderRequest[] = [
        {
          method: "Card",
          amount: totalAmount,
          providerConfigId,
          externalReference: `POS-TERM-${Date.now().toString().slice(-6)}`,
        },
      ];
      await onCheckout(tenders);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("tender.checkoutFailed"));
    }
  };

  const handleSplitSubmit = async () => {
    if (!isComplete) {
      setError(t("tender.splitIncomplete"));
      return;
    }
    if (splitTenders.some((item) => item.method === "Cash") && !defaultCashAccount?.id) {
      setError(t("tender.noCashAccountAvailable", "No active cash account is configured for this legal entity."));
      return;
    }
    if (splitTenders.some((item) => item.method === "Card") && !providerConfigId) {
      setError(t("tender.noCardProviderAvailable", "Card payment provider is not configured for this legal entity."));
      return;
    }

    setError(null);
    try {
      const tenders: TenderRequest[] = splitTenders.map((item) => ({
        method: item.method,
        amount: item.amount,
        ...(item.method === "Cash" && defaultCashAccount ? { paymentAccountId: defaultCashAccount.id } : {}),
        ...(item.method === "Card" && providerConfigId
          ? { providerConfigId, externalReference: `POS-SPLIT-${Date.now().toString().slice(-6)}` }
          : {}),
      }));
      await onCheckout(tenders);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("tender.checkoutFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("tender.modalTitle")}
      description={t("tender.modalSubtitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Total Banner */}
        <div
          style={{
            padding: "1rem",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-default)",
            background: "var(--color-surface-hover)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span style={{ fontSize: "1rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
            {t("tender.totalDue")}:
          </span>
          <span style={{ fontSize: "1.75rem", fontWeight: 800, color: "var(--color-brand-primary)" }}>
            <BidiText>{totalAmount.toFixed(2)} {currencyCode}</BidiText>
          </span>
        </div>

        {/* Method Tabs */}
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={activeTab === "cash" ? "primary" : "secondary"}
            onClick={() => {
              setActiveTab("cash");
              setError(null);
            }}
            style={{ flex: 1, display: "flex", gap: "0.5rem", justifyContent: "center", alignItems: "center" }}
          >
            <CashIcon size={18} />
            <span>{t("tender.cash")}</span>
          </Button>

          <Button
            variant={activeTab === "card" ? "primary" : "secondary"}
            onClick={() => {
              setActiveTab("card");
              setError(null);
            }}
            style={{ flex: 1, display: "flex", gap: "0.5rem", justifyContent: "center", alignItems: "center" }}
          >
            <CreditCardIcon size={18} />
            <span>{t("tender.card")}</span>
          </Button>

          <Button
            variant={activeTab === "split" ? "primary" : "secondary"}
            onClick={() => {
              setActiveTab("split");
              setError(null);
            }}
            style={{ flex: 1, display: "flex", gap: "0.5rem", justifyContent: "center", alignItems: "center" }}
          >
            <SplitIcon size={18} />
            <span>{t("tender.split")}</span>
          </Button>
        </div>

        {/* Cash Tab Content */}
        {activeTab === "cash" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <TextField
              label={t("tender.cashTendered")}
              type="number"
              value={cashTenderedStr}
              onChange={(v) => {
                setCashTenderedStr(v);
                setError(null);
              }}
              autoFocus
            />

            {/* Quick cash bill buttons */}
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              {[totalAmount, Math.ceil(totalAmount / 10) * 10, 50, 100, 200, 500].map((amt, idx) => (
                <Button
                  key={idx}
                  size="compact"
                  variant="secondary"
                  onClick={() => handleQuickCash(amt)}
                >
                  <BidiText>{amt.toFixed(0)} {currencyCode}</BidiText>
                </Button>
              ))}
            </div>

            {/* Change Due Display */}
            <div
              style={{
                padding: "0.75rem 1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: changeAmount > 0 ? "rgba(16, 185, 129, 0.1)" : "var(--color-surface-hover)",
                border: "1px solid",
                borderColor: changeAmount > 0 ? "rgba(16, 185, 129, 0.3)" : "var(--color-border-default)",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <span style={{ fontWeight: 600, color: "var(--color-content-muted)" }}>
                {t("tender.changeDue")}:
              </span>
              <span
                data-testid="tender-change-amount"
                style={{
                  fontSize: "1.25rem",
                  fontWeight: 800,
                  color: changeAmount > 0 ? "var(--color-status-success)" : "var(--color-content-default)",
                }}
              >
                <BidiText>{changeStr} {currencyCode}</BidiText>
              </span>
            </div>

            {defaultCashAccount && (
              <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {t("tender.accountLabel", "حساب الخزينة")}: <BidiText>{defaultCashAccount.name} ({defaultCashAccount.code})</BidiText>
              </div>
            )}
          </div>
        )}

        {/* Card Tab Content */}
        {activeTab === "card" && (
          <div
            style={{
              padding: "1.5rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-surface-hover)",
              textAlign: "center",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem",
              alignItems: "center",
            }}
          >
            <CreditCardIcon size={36} style={{ color: "var(--color-brand-primary)" }} />
            <div style={{ fontWeight: 600 }}>{t("tender.cardPrompt")}</div>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
              {t("tender.cardNotice")}
            </div>
            {providerConfigId && (
              <div style={{ fontSize: "0.75rem", color: "var(--color-status-success)" }}>
                ✓ {t("tender.providerConnected", "مزود الدفع الإلكتروني متصل وجاهز")}
              </div>
            )}
          </div>
        )}

        {/* Split Tab Content */}
        {activeTab === "split" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", gap: "0.5rem" }}>
              <select
                value={splitCurrentMethod}
                onChange={(e) => setSplitCurrentMethod(e.target.value as PosTenderMethod)}
                style={{
                  padding: "0.5rem",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-default)",
                  color: "var(--color-content-default)",
                  fontSize: "0.875rem",
                }}
              >
                <option value="Cash">{t("tender.cash")}</option>
                <option value="Card">{t("tender.card")}</option>
              </select>

              <TextField
                placeholder={t("tender.tenderAmount")}
                value={splitAmountStr}
                onChange={(v) => setSplitAmountStr(v)}
              />

              <Button variant="secondary" onClick={handleAddSplitTender}>
                + {t("tender.addTender")}
              </Button>
            </div>

            {/* Split Tenders List */}
            {splitTenders.length > 0 && (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                {splitTenders.map((item, idx) => (
                  <div
                    key={idx}
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      padding: "0.5rem 0.75rem",
                      borderRadius: "var(--radius-sm)",
                      backgroundColor: "var(--color-surface-hover)",
                      border: "1px solid var(--color-border-default)",
                      fontSize: "0.875rem",
                    }}
                  >
                    <span>
                      {item.method === "Cash" ? t("tender.cash") : t("tender.card")}:{" "}
                      <strong><BidiText>{item.amountStr} {currencyCode}</BidiText></strong>
                    </span>
                    <button
                      type="button"
                      onClick={() => handleRemoveSplitTender(idx)}
                      style={{
                        background: "none",
                        border: "none",
                        color: "var(--color-status-danger)",
                        cursor: "pointer",
                        fontWeight: 700,
                      }}
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}

            {/* Split Status */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                padding: "0.5rem 0",
                fontSize: "0.875rem",
                fontWeight: 600,
              }}
            >
              <span>{t("tender.paid")}: <BidiText>{paidTotalStr} {currencyCode}</BidiText></span>
              <span style={{ color: remainingAmount > 0 ? "var(--color-status-danger)" : "var(--color-status-success)" }}>
                {t("tender.remaining")}: <BidiText>{remainingStr} {currencyCode}</BidiText>
              </span>
            </div>
          </div>
        )}

        {/* Actionable Error Message */}
        {error && (
          <div
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "rgba(239, 68, 68, 0.1)",
              border: "1px solid rgba(239, 68, 68, 0.3)",
              color: "var(--color-status-danger)",
              fontSize: "0.875rem",
            }}
            role="alert"
          >
            {error}
          </div>
        )}

        {/* Modal Footer Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>

          {activeTab === "cash" && (
            <Button
              variant="primary"
              onClick={handleCashSubmit}
              isDisabled={isSubmitting || !isSufficient || !defaultCashAccount}
            >
              {isSubmitting ? t("common.submitting") : t("tender.confirmCashCheckout")}
            </Button>
          )}

          {activeTab === "card" && (
            <Button
              variant="primary"
              onClick={handleCardSubmit}
              isDisabled={isSubmitting || !providerConfigId}
            >
              {isSubmitting ? t("common.submitting") : t("tender.confirmCardCheckout")}
            </Button>
          )}

          {activeTab === "split" && (
            <Button
              variant="primary"
              onClick={handleSplitSubmit}
              isDisabled={isSubmitting || !isComplete}
            >
              {isSubmitting ? t("common.submitting") : t("tender.confirmSplitCheckout")}
            </Button>
          )}
        </div>
      </div>
    </Dialog>
  );
}
