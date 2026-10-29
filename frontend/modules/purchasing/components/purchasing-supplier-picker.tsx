import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  KNOWN_PURCHASING_SUPPLIERS,
  resolvePurchasingSupplier,
  type ResolvedSupplier
} from "./purchasing-supplier-resolver.ts";

export interface PurchasingSupplierPickerProps {
  readonly value?: string | null | undefined;
  readonly onChange: (supplierPartyId: string) => void;
  readonly isRequired?: boolean | undefined;
  readonly label?: string | undefined;
  readonly disabled?: boolean | undefined;
}

export function PurchasingSupplierPicker({
  value,
  onChange,
  isRequired = false,
  label,
  disabled = false
}: PurchasingSupplierPickerProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";

  const [searchQuery, setSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [isQuickCreateOpen, setIsQuickCreateOpen] = useState(false);
  const [quickName, setQuickName] = useState("");
  const [quickPhone, setQuickPhone] = useState("");
  const [quickTax, setQuickTax] = useState("");
  const [extraSuppliers, setExtraSuppliers] = useState<Record<string, ResolvedSupplier>>({});

  const selectedSupplier = value
    ? extraSuppliers[value] ?? resolvePurchasingSupplier(value, isAr)
    : null;

  const allSuppliers = [
    ...Object.values(KNOWN_PURCHASING_SUPPLIERS),
    ...Object.values(extraSuppliers)
  ];

  const filteredSuppliers = searchQuery.trim()
    ? allSuppliers.filter((s) => {
        const q = searchQuery.toLowerCase().trim();
        return (
          s.nameAr.toLowerCase().includes(q) ||
          s.nameEn.toLowerCase().includes(q) ||
          s.code.toLowerCase().includes(q) ||
          (s.taxNumber && s.taxNumber.includes(q)) ||
          (s.phone && s.phone.includes(q))
        );
      })
    : allSuppliers;

  const handleSelect = (supplier: ResolvedSupplier) => {
    if (supplier.blocked) return;
    onChange(supplier.id);
    setIsSearching(false);
    setSearchQuery("");
  };

  const handleQuickCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!quickName.trim()) return;
    const newId = `party-supplier-${Date.now().toString(36)}`;
    const newCode = `SUP-${Math.floor(1000 + Math.random() * 9000)}`;
    const created: ResolvedSupplier = {
      id: newId,
      name: quickName.trim(),
      nameAr: quickName.trim(),
      nameEn: quickName.trim(),
      code: newCode,
      taxNumber: quickTax.trim() || undefined,
      phone: quickPhone.trim() || undefined,
      nature: "Trading"
    };
    setExtraSuppliers((prev) => ({ ...prev, [newId]: created }));
    onChange(newId);
    setIsQuickCreateOpen(false);
    setIsSearching(false);
    setQuickName("");
    setQuickPhone("");
    setQuickTax("");
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
        {label ?? t("orders.supplier")}
        {isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
      </label>

      {/* Selected Supplier Card */}
      {selectedSupplier && value ? (
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
              {selectedSupplier.name}
            </span>
            <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <span style={{ fontFamily: "monospace", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {selectedSupplier.code}
              </span>
              {selectedSupplier.taxNumber && (
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                  · {selectedSupplier.taxNumber}
                </span>
              )}
              {selectedSupplier.phone && (
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }} dir="ltr">
                  · {selectedSupplier.phone}
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
          {t("common.search")} / {t("orders.supplier")}
        </Button>
      )}

      {/* Supplier Selection Modal / Drawer */}
      {isSearching && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: "rgba(15, 23, 42, 0.6)",
            backdropFilter: "blur(2px)",
            display: "grid",
            placeItems: "center",
            zIndex: 1000,
            padding: "1rem"
          }}
        >
          <div
            style={{
              backgroundColor: "var(--color-surface-card, #ffffff)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              borderRadius: "var(--radius-lg, 0.75rem)",
              boxShadow: "var(--shadow-elevation-high)",
              width: "100%",
              maxWidth: "38rem",
              maxHeight: "85vh",
              display: "flex",
              flexDirection: "column",
              overflow: "hidden"
            }}
          >
            {/* Header */}
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "1rem 1.25rem",
                borderBottom: "1px solid var(--color-border-default)"
              }}
            >
              <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                {t("suppliers.selectTitle")}
              </h2>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <Button
                  variant="primary"
                  onPress={() => setIsQuickCreateOpen(true)}
                  type="button"
                >
                  + {t("suppliers.quickCreate")}
                </Button>
                <Button
                  variant="secondary"
                  onPress={() => setIsSearching(false)}
                  type="button"
                >
                  {t("common.close")}
                </Button>
              </div>
            </div>

            {/* Search Input */}
            <div style={{ padding: "1rem 1.25rem 0.5rem" }}>
              <SearchField
                value={searchQuery}
                onChange={setSearchQuery}
                placeholder={t("suppliers.searchPlaceholder")}
                aria-label={t("suppliers.searchPlaceholder")}
              />
            </div>

            {/* List */}
            <div style={{ flex: 1, overflowY: "auto", padding: "0.5rem 1.25rem 1.25rem" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                {filteredSuppliers.map((supplier) => (
                  <div
                    key={supplier.id}
                    onClick={() => handleSelect(supplier)}
                    role="button"
                    tabIndex={supplier.blocked ? -1 : 0}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleSelect(supplier);
                      }
                    }}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      padding: "0.75rem 1rem",
                      borderRadius: "var(--radius-md, 0.5rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: supplier.blocked
                        ? "var(--color-surface-subtle, #f8fafc)"
                        : value === supplier.id
                        ? "var(--color-brand-primary-light, #eff6ff)"
                        : "var(--color-surface-card, #ffffff)",
                      opacity: supplier.blocked ? 0.6 : 1,
                      cursor: supplier.blocked ? "not-allowed" : "pointer",
                      transition: "background-color 0.15s ease"
                    }}
                  >
                    <div style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
                      <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                        <span style={{ fontWeight: 600, fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>
                          {supplier.name}
                        </span>
                        {supplier.blocked && (
                          <span style={{ fontSize: "0.75rem", color: "var(--color-status-danger)", fontWeight: 600 }}>
                            ({t("suppliers.blocked")})
                          </span>
                        )}
                      </div>
                      <div style={{ display: "flex", gap: "0.625rem", alignItems: "center", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        <span style={{ fontFamily: "monospace" }}>{supplier.code}</span>
                        {supplier.taxNumber && <span>{supplier.taxNumber}</span>}
                        {supplier.phone && <span dir="ltr">{supplier.phone}</span>}
                      </div>
                    </div>

                    <Button
                      variant={value === supplier.id ? "primary" : "secondary"}
                      isDisabled={supplier.blocked}
                      onPress={() => handleSelect(supplier)}
                      type="button"
                    >
                      {value === supplier.id ? t("common.selected") : t("common.select")}
                    </Button>
                  </div>
                ))}

                {filteredSuppliers.length === 0 && (
                  <div style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
                    <p style={{ margin: "0 0 1rem" }}>{t("suppliers.noResults")}</p>
                    <Button
                      variant="primary"
                      onPress={() => setIsQuickCreateOpen(true)}
                      type="button"
                    >
                      + {t("suppliers.quickCreate")}
                    </Button>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Quick Create Supplier Modal */}
      {isQuickCreateOpen && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: "rgba(15, 23, 42, 0.7)",
            backdropFilter: "blur(2px)",
            display: "grid",
            placeItems: "center",
            zIndex: 1100,
            padding: "1rem"
          }}
        >
          <div
            style={{
              backgroundColor: "var(--color-surface-card, #ffffff)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              borderRadius: "var(--radius-lg, 0.75rem)",
              boxShadow: "var(--shadow-elevation-high)",
              width: "100%",
              maxWidth: "32rem",
              padding: "1.5rem"
            }}
          >
            <h3 style={{ margin: "0 0 1rem", fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("suppliers.quickCreateTitle")}
            </h3>
            <form onSubmit={handleQuickCreate} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-primary)" }}>
                  {t("suppliers.name")} *
                </label>
                <input
                  type="text"
                  required
                  value={quickName}
                  onChange={(e) => setQuickName(e.target.value)}
                  placeholder={t("suppliers.namePlaceholder")}
                  style={{
                    width: "100%",
                    padding: "0.5rem 0.75rem",
                    border: "1px solid var(--color-border-default)",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "0.875rem",
                    backgroundColor: "var(--color-surface-card)",
                    color: "var(--color-content-primary)"
                  }}
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-primary)" }}>
                  {t("suppliers.taxNumber")}
                </label>
                <input
                  type="text"
                  value={quickTax}
                  onChange={(e) => setQuickTax(e.target.value)}
                  placeholder={t("suppliers.taxNumberPlaceholder")}
                  style={{
                    width: "100%",
                    padding: "0.5rem 0.75rem",
                    border: "1px solid var(--color-border-default)",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "0.875rem",
                    backgroundColor: "var(--color-surface-card)",
                    color: "var(--color-content-primary)"
                  }}
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-primary)" }}>
                  {t("suppliers.phone")}
                </label>
                <input
                  type="tel"
                  value={quickPhone}
                  onChange={(e) => setQuickPhone(e.target.value)}
                  placeholder={t("suppliers.phonePlaceholder")}
                  dir="ltr"
                  style={{
                    width: "100%",
                    padding: "0.5rem 0.75rem",
                    border: "1px solid var(--color-border-default)",
                    borderRadius: "var(--radius-sm)",
                    fontSize: "0.875rem",
                    backgroundColor: "var(--color-surface-card)",
                    color: "var(--color-content-primary)"
                  }}
                />
              </div>

              <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
                <Button
                  variant="secondary"
                  onPress={() => setIsQuickCreateOpen(false)}
                  type="button"
                >
                  {t("common.cancel")}
                </Button>
                <Button
                  variant="primary"
                  type="submit"
                >
                  {t("common.save")}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
