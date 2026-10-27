import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { paymentApi } from "../services/payment-api.ts";
import type { PublicPaymentLinkDetails } from "../types.ts";

export function PublicPaymentLinkPage(): React.JSX.Element {
  const { t } = useTranslation("payments");
  const { token } = useParams<{ token: string }>();

  const [details, setDetails] = useState<PublicPaymentLinkDetails | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    if (!token) return;
    setLoading(true);
    paymentApi
      .getPublicPaymentLink(token)
      .then((res) => {
        setDetails(res);
        setLoading(false);
      })
      .catch((err) => {
        setError((err as Error).message);
        setLoading(false);
      });
  }, [token]);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", backgroundColor: "#f0f4f8" }}>
        <LoadingState message={t("link.loading")} />
      </div>
    );
  }

  if (error || !details) {
    return (
      <div style={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", backgroundColor: "#f0f4f8" }}>
        <ErrorState title={t("link.invalidTitle")} message={t("link.invalidDesc")} />
      </div>
    );
  }

  const isExpired = new Date(details.expiresAt).getTime() < Date.now();

  return (
    <div
      style={{
        minHeight: "100vh",
        backgroundColor: "#f0f4f8",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        padding: "1.5rem"
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth: "480px",
          backgroundColor: "#ffffff",
          borderRadius: "8px",
          border: "1px solid #cbd5e1",
          boxShadow: "0 4px 12px -2px rgba(16, 42, 67, 0.1)",
          overflow: "hidden"
        }}
      >
        {/* Top Header */}
        <div style={{ backgroundColor: "#102a43", color: "#ffffff", padding: "1.25rem 1.5rem", textAlign: "center" }}>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 800 }}>{t("link.checkoutTitle")}</h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "#93c5fd" }}>
            {t("link.securePortal")}
          </p>
        </div>

        {/* Content */}
        <div style={{ padding: "1.5rem", textAlign: "center" }}>
          <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("link.amountDue")}:</span>
          <div style={{ fontSize: "2rem", fontWeight: 800, color: "#102a43", margin: "0.5rem 0 1rem 0" }}>
            <MoneyDisplay value={{ amount: String(details.amount), currency: details.currencyCode as any }} />
          </div>

          <div
            style={{
              display: "inline-block",
              padding: "0.25rem 0.75rem",
              borderRadius: "9999px",
              fontSize: "0.75rem",
              fontWeight: 700,
              backgroundColor: isExpired ? "#fef2f2" : "#ecfdf5",
              color: isExpired ? "#991b1b" : "#065f46",
              marginBottom: "1.5rem"
            }}
          >
            {isExpired ? t("link.statusExpired") : t("link.statusActive")}
          </div>

          <div style={{ textAlign: "start", borderTop: "1px solid #e2e8f0", paddingTop: "1rem", fontSize: "0.8125rem", color: "#475569" }}>
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.5rem" }}>
              <span>{t("link.currency")}:</span>
              <strong>{details.currencyCode}</strong>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.5rem" }}>
              <span>{t("link.expiresAt")}:</span>
              <span>{new Date(details.expiresAt).toLocaleDateString()}</span>
            </div>
          </div>

          {!isExpired && (
            <div style={{ marginTop: "1.5rem" }}>
              <Button
                variant="primary"
                onPress={() => {
                  alert(t("link.gatewayRedirectNotice"));
                }}
              >
                {t("link.payNowAction")}
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
