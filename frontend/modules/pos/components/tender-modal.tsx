import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { CashIcon, CreditCardIcon, SplitIcon } from "./pos-icons.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";
import type { PosTenderMethod, TenderRequest } from "../types.ts";

export interface TenderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly totalAmount: number;
  readonly currencyCode?: string | undefined;
  readonly onCheckout: (tenders: readonly TenderRequest[]) => Promise<void>;
  readonly isSubmitting?: boolean | undefined;
}

export function TenderModal({
  isOpen,
  onClose,
  totalAmount,
  currencyCode = "EGP",
  onCheckout,
  isSubmitting = false,
}: TenderModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");
  const [activeTab, setActiveTab] = useState<"cash" | "card" | "split">("cash");

  // Cash state
  const [cashTenderedStr, setCashTenderedStr] = useState<string>(totalAmount > 0 ? totalAmount.toFixed(2) : "0");

  // Split state
  const [splitTenders, setSplitTenders] = useState<Array<{ method: PosTenderMethod; amount: number }>>([]);
  const [splitCurrentMethod, setSplitCurrentMethod] = useState<PosTenderMethod>("Cash");
  const [splitAmountStr, setSplitAmountStr] = useState<string>("");

  const [error, setError] = useState<string | null>(null);

  // Calculations
  const cashTendered = parseFloat(cashTenderedStr) || 0;
  const cashChange = Math.max(0, Math.round((cashTendered - totalAmount) * 100) / 100);

  const splitPaidTotal = splitTenders.reduce((sum, item) => sum + item.amount, 0);
  const splitRemaining = Math.max(0, Math.round((totalAmount - splitPaidTotal) * 100) / 100);

  const handleQuickCash = (amount: number) => {
    setCashTenderedStr(amount.toFixed(2));
    setError(null);
  };

  const handleAddSplitTender = () => {
    const amt = parseFloat(splitAmountStr) || 0;
    if (amt <= 0) return;
    setSplitTenders([...splitTenders, { method: splitCurrentMethod, amount: amt }]);
    setSplitAmountStr("");
  };

  const handleRemoveSplitTender = (idx: number) => {
    setSplitTenders(splitTenders.filter((_, i) => i !== idx));
  };

  const handleCashSubmit = async () => {
    if (cashTendered < totalAmount) {
      setError(t("tender.insufficientCash"));
      return;
    }
    setError(null);
    try {
      const tenders: TenderRequest[] = [
        {
          method: "Cash",
          amount: totalAmount,
          paymentAccountId: "6f72315c-5930-765b-bc01-79915b2f304a",
        },
      ];
      await onCheckout(tenders);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("tender.checkoutFailed"));
    }
  };

  const handleCardSubmit = async () => {
    setError(null);
    try {
      const tenders: TenderRequest[] = [
        {
          method: "Card",
          amount: totalAmount,
          providerConfigId: "ea7b5b9d-b2b9-8858-89b2-3858fa6127b9",
          externalReference: "POS-TERM-001",
        },
      ];
      await onCheckout(tenders);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("tender.checkoutFailed"));
    }
  };

  const handleSplitSubmit = async () => {
    if (splitPaidTotal < totalAmount) {
      setError(t("tender.splitIncomplete"));
      return;
    }
    setError(null);
    try {
      const tenders: TenderRequest[] = splitTenders.map((item) => ({
        method: item.method,
        amount: item.amount,
        ...(item.method === "Cash" ? { paymentAccountId: "6f72315c-5930-765b-bc01-79915b2f304a" } : {}),
        ...(item.method === "Card" ? { providerConfigId: "ea7b5b9d-b2b9-8858-89b2-3858fa6127b9", externalReference: "POS-SPLIT-CARD" } : {}),
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
            onClick={() => setActiveTab("cash")}
            style={{ flex: 1, justifyContent: "center", display: "flex", gap: "0.5rem", alignItems: "center" }}
          >
            <CashIcon size={18} />
            <span>{t("tender.cash")}</span>
          </Button>
          <Button
            variant={activeTab === "card" ? "primary" : "secondary"}
            onClick={() => setActiveTab("card")}
            style={{ flex: 1, justifyContent: "center", display: "flex", gap: "0.5rem", alignItems: "center" }}
          >
            <CreditCardIcon size={18} />
            <span>{t("tender.card")}</span>
          </Button>
          <Button
            variant={activeTab === "split" ? "primary" : "secondary"}
            onClick={() => {
              setActiveTab("split");
              setSplitAmountStr(splitRemaining.toFixed(2));
            }}
            style={{ flex: 1, justifyContent: "center", display: "flex", gap: "0.5rem", alignItems: "center" }}
          >
            <SplitIcon size={18} />
            <span>{t("tender.split")}</span>
          </Button>
        </div>

        {/* Cash Tab Content */}
        {activeTab === "cash" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <TextField
              label={`${t("tender.cashTendered")} (${currencyCode})`}
              type="number"
              value={cashTenderedStr}
              onChange={(v) => {
                setCashTenderedStr(v);
                setError(null);
              }}
              autoFocus
            />

            {/* Quick cash denomination chips */}
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              <Button size="compact" variant="secondary" onClick={() => handleQuickCash(totalAmount)}>
                {t("tender.exactAmount")} (<BidiText>{totalAmount.toFixed(2)}</BidiText>)
              </Button>
              {[50, 100, 200, 500].map((amt) => {
                if (amt < totalAmount) return null;
                return (
                  <Button key={amt} size="compact" variant="secondary" onClick={() => handleQuickCash(amt)}>
                    <BidiText>{amt} {currencyCode}</BidiText>
                  </Button>
                );
              })}
            </div>

            {/* Change returned display */}
            <div
              style={{
                padding: "0.75rem 1rem",
                borderRadius: "var(--radius-md)",
                backgroundColor: cashChange > 0 ? "rgba(16, 185, 129, 0.1)" : "var(--color-surface-hover)",
                border: "1px solid var(--color-border-subtle)",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <span style={{ fontWeight: 600 }}>{t("tender.changeDue")}:</span>
              <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-status-success)" }}>
                <BidiText>{cashChange.toFixed(2)} {currencyCode}</BidiText>
              </span>
            </div>

            <Button
              variant="primary"
              size="comfortable"
              isDisabled={isSubmitting || cashTendered < totalAmount}
              onClick={handleCashSubmit}
              style={{ width: "100%", justifyContent: "center", marginTop: "0.5rem" }}
            >
              {isSubmitting ? t("common.submitting") : t("tender.completeCashSale")}
            </Button>
          </div>
        )}

        {/* Card Tab Content */}
        {activeTab === "card" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div
              style={{
                padding: "1.5rem",
                textAlign: "center",
                borderRadius: "var(--radius-md)",
                border: "1px dashed var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)",
              }}
            >
              <CreditCardIcon size={40} style={{ margin: "0 auto 0.75rem", color: "var(--color-brand-primary)" }} />
              <div style={{ fontWeight: 600, fontSize: "1.125rem" }}>
                {t("tender.terminalSwipePrompt")}
              </div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", marginTop: "0.25rem" }}>
                {t("tender.terminalAuthReady")}
              </div>
            </div>

            <Button
              variant="primary"
              size="comfortable"
              isDisabled={isSubmitting}
              onClick={handleCardSubmit}
              style={{ width: "100%", justifyContent: "center" }}
            >
              {isSubmitting ? t("common.submitting") : `${t("tender.chargeCard")} (${totalAmount.toFixed(2)} ${currencyCode})`}
            </Button>
          </div>
        )}

        {/* Split Tab Content */}
        {activeTab === "split" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", gap: "0.5rem", alignItems: "flex-end" }}>
              <div style={{ width: "8rem" }}>
                <label style={{ fontSize: "0.8125rem", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
                  {t("tender.method")}
                </label>
                <select
                  value={splitCurrentMethod}
                  onChange={(e) => setSplitCurrentMethod(e.target.value as PosTenderMethod)}
                  style={{
                    width: "100%",
                    padding: "0.5rem",
                    borderRadius: "var(--radius-sm)",
                    border: "1px solid var(--color-border-default)",
                    background: "var(--color-surface-card)",
                    color: "var(--color-content-default)",
                  }}
                >
                  <option value="Cash">{t("tender.cash")}</option>
                  <option value="Card">{t("tender.card")}</option>
                  <option value="CustomerCredit">{t("tender.credit")}</option>
                </select>
              </div>

              <div style={{ flex: 1 }}>
                <TextField
                  label={`${t("tender.amount")} (${currencyCode})`}
                  type="number"
                  value={splitAmountStr}
                  onChange={(v) => setSplitAmountStr(v)}
                />
              </div>

              <Button
                variant="secondary"
                onClick={handleAddSplitTender}
                isDisabled={parseFloat(splitAmountStr) <= 0}
              >
                {t("tender.addPayment")}
              </Button>
            </div>

            {/* Split Tenders List */}
            {splitTenders.length > 0 && (
              <div style={{ border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)" }}>
                {splitTenders.map((tItem, idx) => (
                  <div
                    key={idx}
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      padding: "0.5rem 0.75rem",
                      borderBottom: idx === splitTenders.length - 1 ? "none" : "1px solid var(--color-border-subtle)",
                    }}
                  >
                    <span>{tItem.method === "Cash" ? t("tender.cash") : tItem.method === "Card" ? t("tender.card") : t("tender.credit")}</span>
                    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                      <strong style={{ color: "var(--color-content-default)" }}>
                        <BidiText>{tItem.amount.toFixed(2)} {currencyCode}</BidiText>
                      </strong>
                      <Button size="compact" variant="secondary" onClick={() => handleRemoveSplitTender(idx)}>
                        ×
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.875rem" }}>
              <span>{t("tender.remainingDue")}:</span>
              <strong style={{ color: splitRemaining > 0 ? "var(--color-status-danger)" : "var(--color-status-success)" }}>
                <BidiText>{splitRemaining.toFixed(2)} {currencyCode}</BidiText>
              </strong>
            </div>

            <Button
              variant="primary"
              size="comfortable"
              isDisabled={isSubmitting || splitPaidTotal < totalAmount}
              onClick={handleSplitSubmit}
              style={{ width: "100%", justifyContent: "center" }}
            >
              {isSubmitting ? t("common.submitting") : t("tender.completeSplitSale")}
            </Button>
          </div>
        )}

        {error && (
          <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
            {error}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="secondary" onClick={onClose} isDisabled={isSubmitting}>
            {t("common.cancel")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
