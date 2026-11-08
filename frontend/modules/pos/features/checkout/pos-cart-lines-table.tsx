import React from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import type { CartLineResponse } from "../../types.ts";

export interface PosCartLinesTableProps {
  readonly lines: readonly CartLineResponse[];
  readonly currencyCode?: string | undefined;
  readonly onUpdateQuantity: (lineId: string, currentQty: number, delta: number, version: number) => void;
  readonly onOpenDiscount: (line: CartLineResponse) => void;
  readonly onRemoveLine: (lineId: string, version: number) => void;
  readonly cartVersion: number;
  readonly disabled?: boolean | undefined;
}

export function PosCartLinesTable({
  lines,
  currencyCode = "EGP",
  onUpdateQuantity,
  onOpenDiscount,
  onRemoveLine,
  cartVersion,
  disabled = false,
}: PosCartLinesTableProps): React.JSX.Element {
  const { t } = useTranslation("pos");

  if (lines.length === 0) {
    return (
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          padding: "3rem 1.5rem",
          color: "var(--color-content-muted)",
          textAlign: "center",
          backgroundColor: "#ffffff",
          borderRadius: "var(--radius-md)",
          border: "1px dashed #cbd5e1",
          minHeight: "16rem",
        }}
      >
        <div style={{ fontSize: "1.125rem", fontWeight: 600, color: "var(--color-content-default)" }}>
          {t("cart.emptyTitle")}
        </div>
        <p style={{ margin: "0.5rem 0 0", fontSize: "0.875rem" }}>
          {t("cart.emptyPrompt")}
        </p>
      </div>
    );
  }

  return (
    <div
      style={{
        overflowX: "auto",
        backgroundColor: "#ffffff",
        borderRadius: "var(--radius-md)",
        border: "1px solid #cbd5e1",
        boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
      }}
    >
      <table
        style={{
          width: "100%",
          borderCollapse: "collapse",
          fontSize: "0.875rem",
          textAlign: "start",
        }}
      >
        <thead>
          <tr
            style={{
              backgroundColor: "var(--color-surface-hover)",
              borderBottom: "1px solid #cbd5e1",
              color: "var(--color-content-muted)",
              fontWeight: 600,
            }}
          >
            <th style={{ padding: "0.75rem 1rem", width: "3.5rem", textAlign: "center" }}>#</th>
            <th style={{ padding: "0.75rem 1rem", minWidth: "16rem" }}>{t("cart.colItem")}</th>
            <th style={{ padding: "0.75rem 1rem", textAlign: "end", width: "8rem" }}>{t("cart.colPrice")}</th>
            <th style={{ padding: "0.75rem 1rem", textAlign: "center", width: "9.5rem" }}>{t("cart.colQty")}</th>
            <th style={{ padding: "0.75rem 1rem", textAlign: "end", width: "7.5rem" }}>{t("cart.colDiscount")}</th>
            <th style={{ padding: "0.75rem 1rem", textAlign: "end", width: "8.5rem" }}>{t("cart.colTotal")}</th>
            <th style={{ padding: "0.75rem 0.5rem", width: "3.5rem", textAlign: "center" }} aria-label={t("common.actions")}></th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line, idx) => (
            <tr
              key={line.id}
              style={{
                borderBottom: idx === lines.length - 1 ? "none" : "1px solid #e2e8f0",
                transition: "background-color 0.15s ease",
              }}
            >
              <td style={{ padding: "0.75rem 1rem", textAlign: "center", color: "var(--color-content-muted)", fontWeight: 500 }}>
                {idx + 1}
              </td>
              <td style={{ padding: "0.75rem 1rem", minWidth: "16rem" }}>
                <div style={{ fontWeight: 600, color: "var(--color-content-default)", lineHeight: 1.45, wordBreak: "break-word" }}>
                  <BidiText>{line.description}</BidiText>
                </div>
                <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginTop: "0.2rem" }}>
                  <span style={{ fontWeight: 500 }}>{t("cart.uom")}: </span><BidiText>{line.uomCode || ""}</BidiText>
                  {line.taxAmount > 0 && (
                    <span style={{ marginInlineStart: "0.75rem", color: "var(--color-brand-primary)" }}>
                      {t("cart.tax")}: <BidiText>{line.taxAmount.toFixed(2)} {currencyCode}</BidiText>
                    </span>
                  )}
                </div>
              </td>
              <td style={{ padding: "0.75rem 1rem", textAlign: "end", whiteSpace: "nowrap" }}>
                <BidiText>{line.unitPrice.toFixed(2)} {currencyCode}</BidiText>
              </td>
              <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                <div
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    border: "1px solid #cbd5e1",
                    borderRadius: "var(--radius-sm)",
                    backgroundColor: "#ffffff",
                    overflow: "hidden",
                  }}
                >
                  <button
                    type="button"
                    disabled={disabled || line.quantity <= 1}
                    onClick={() => onUpdateQuantity(line.id, line.quantity, -1, cartVersion)}
                    style={{
                      border: "none",
                      background: "transparent",
                      padding: "0.35rem 0.6rem",
                      cursor: disabled || line.quantity <= 1 ? "not-allowed" : "pointer",
                      fontWeight: 700,
                      color: "var(--color-content-default)",
                    }}
                    aria-label={t("cart.decreaseQty")}
                  >
                    -
                  </button>
                  <span
                    style={{
                      padding: "0.35rem 0.75rem",
                      fontWeight: 700,
                      minWidth: "2rem",
                      textAlign: "center",
                      backgroundColor: "var(--color-surface-page)",
                      borderInline: "1px solid #e2e8f0",
                    }}
                  >
                    <BidiText>{line.quantity}</BidiText>
                  </span>
                  <button
                    type="button"
                    disabled={disabled}
                    onClick={() => onUpdateQuantity(line.id, line.quantity, 1, cartVersion)}
                    style={{
                      border: "none",
                      background: "transparent",
                      padding: "0.35rem 0.6rem",
                      cursor: disabled ? "not-allowed" : "pointer",
                      fontWeight: 700,
                      color: "var(--color-content-default)",
                    }}
                    aria-label={t("cart.increaseQty")}
                  >
                    +
                  </button>
                </div>
              </td>
              <td style={{ padding: "0.75rem 1rem", textAlign: "end", whiteSpace: "nowrap" }}>
                {line.discountAmount > 0 ? (
                  <Button
                    size="compact"
                    variant="secondary"
                    onClick={() => onOpenDiscount(line)}
                    style={{ color: "var(--color-status-success)", borderColor: "rgba(16, 185, 129, 0.3)" }}
                  >
                    -<BidiText>{line.discountAmount.toFixed(2)}</BidiText>
                  </Button>
                ) : (
                  <Button
                    size="compact"
                    variant="ghost"
                    onClick={() => onOpenDiscount(line)}
                    style={{ color: "var(--color-content-muted)" }}
                  >
                    {t("cart.addDiscount")}
                  </Button>
                )}
              </td>
              <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-content-default)", whiteSpace: "nowrap" }}>
                <BidiText>{line.lineTotal.toFixed(2)} {currencyCode}</BidiText>
              </td>
              <td style={{ padding: "0.75rem 0.5rem", textAlign: "center" }}>
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => onRemoveLine(line.id, cartVersion)}
                  style={{
                    background: "none",
                    border: "none",
                    color: "var(--color-status-danger)",
                    cursor: disabled ? "not-allowed" : "pointer",
                    padding: "0.25rem 0.5rem",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "1.125rem",
                    lineHeight: 1,
                  }}
                  title={t("cart.removeLine")}
                  aria-label={t("cart.removeLine")}
                >
                  ×
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
