import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  KNOWN_SALES_CUSTOMERS,
  resolveSalesCustomer,
  type ResolvedCustomer
} from "./sales-customer-display.tsx";

export interface SalesCustomerPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (partyId: string) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export function SalesCustomerPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: SalesCustomerPickerProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);

  const selectedCustomer = value ? resolveSalesCustomer(value, isAr) : null;
  const customerList = Object.values(KNOWN_SALES_CUSTOMERS);

  const filteredCustomers = searchQuery.trim()
    ? customerList.filter((c) => {
        const q = searchQuery.toLowerCase().trim();
        return (
          c.nameAr.toLowerCase().includes(q) ||
          c.nameEn.toLowerCase().includes(q) ||
          (c.commercialNumber && c.commercialNumber.toLowerCase().includes(q)) ||
          (c.phone && c.phone.includes(q))
        );
      })
    : customerList;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
        {label ?? t("quotations.customer")}
        {isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
      </label>

      {/* Selected Customer Card */}
      {selectedCustomer && value ? (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0.625rem 0.875rem",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-md, 0.5rem)"
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
            <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
              {selectedCustomer.name}
            </span>
            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {selectedCustomer.code}
              </span>
              {selectedCustomer.phone && (
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  · {selectedCustomer.phone}
                </span>
              )}
            </div>
          </div>

          {!disabled && (
            <Button
              variant="secondary"
              onPress={() => setIsSearching(!isSearching)}
              type="button"
            >
              {isSearching ? t("common.close") : t("common.change")}
            </Button>
          )}
        </div>
      ) : (
        <Button
          variant="secondary"
          onPress={() => setIsSearching(true)}
          type="button"
          isDisabled={disabled}
        >
          {t("common.search")} / {t("quotations.customer")}
        </Button>
      )}

      {/* Search Box */}
      {(!value || isSearching) && !disabled && (
        <div
          style={{
            padding: "0.75rem",
            backgroundColor: "var(--color-surface-card)",
            border: "1px dashed var(--color-brand-primary)",
            borderRadius: "var(--radius-md)",
            display: "flex",
            flexDirection: "column",
            gap: "0.625rem"
          }}
        >
          <SearchField
            value={searchQuery}
            onChange={setSearchQuery}
            placeholder={t("quotations.searchCustomerPlaceholder")}
            ariaLabel={t("quotations.customer")}
          />

          {/* Results List */}
          <div style={{ maxHeight: "150px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
            {filteredCustomers.map((c: ResolvedCustomer) => (
              <button
                key={c.id}
                type="button"
                onClick={() => {
                  onChange(c.id);
                  setIsSearching(false);
                  setSearchQuery("");
                }}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  padding: "0.5rem 0.75rem",
                  textAlign: "start",
                  border: "1px solid var(--color-border-subtle, #e2e8f0)",
                  borderRadius: "var(--radius-sm)",
                  backgroundColor: value === c.id ? "var(--color-surface-subtle)" : "transparent",
                  cursor: "pointer",
                  fontSize: "0.8125rem"
                }}
              >
                <div>
                  <strong style={{ display: "block" }}>{isAr ? c.nameAr : c.nameEn}</strong>
                  <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                    {c.commercialNumber ?? c.id}
                  </span>
                </div>
                {c.phone && (
                  <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                    {c.phone}
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
