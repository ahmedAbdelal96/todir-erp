import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { PurchasingSupplierPicker } from "../../components/purchasing-supplier-picker.tsx";
import { PurchasingProductPicker } from "../../components/purchasing-product-picker.tsx";
import { resolvePurchasingProduct } from "../../components/purchasing-product-resolver.ts";
import { toMoneyVal, generateIdempotencyKey, addDaysToIsoDate, resolveConfiguredLeadTimeDays } from "../../utils.ts";
import { mapPurchasingError } from "../../services/purchasing-error-map.ts";
import {
  useCreatePurchaseOrderMutation,
  useApproveAndBillMutation,
  useSupplierProfile,
  usePurchasingSettings
} from "../../hooks/use-purchasing-queries.ts";
import type { PurchaseOrderLineRequest } from "../../types.ts";

export interface NewPurchaseOrderModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly onCreated?: ((orderId: string, invoiceId?: string) => void) | undefined;
  readonly hasPriceOverridePermission?: boolean | undefined;
}

interface DraftLine {
  id: string;
  productVariantId?: string | null;
  description: string;
  quantity: number;
  unitCode: string;
  unitPrice: number;
  priceOverrideReason?: string;
  isStocked?: boolean;
}

export function NewPurchaseOrderModal({
  isOpen,
  onClose,
  legalEntityId,
  onCreated,
  hasPriceOverridePermission = true
}: NewPurchaseOrderModalProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");
  const today = new Date().toISOString().split("T")[0] || "2026-10-29";
  const isAr = document.documentElement.lang !== "en" && document.documentElement.dir !== "ltr";
  const defaultProduct = resolvePurchasingProduct("a6315416-cffe-bf52-bcf0-d5e590d3bf94", isAr);

  const [supplierPartyId, setSupplierPartyId] = useState<string>("");
  const [orderDate, setOrderDate] = useState<string>(today);
  const [currencyCode, setCurrencyCode] = useState<string>("SAR");
  const [expectedDeliveryDate, setExpectedDeliveryDate] = useState<string>("");
  const [isDeliveryDateManual, setIsDeliveryDateManual] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: supplierProfile } = useSupplierProfile(legalEntityId, supplierPartyId);
  const { data: settings } = usePurchasingSettings(legalEntityId);

  // Authoritative configured lead time priority:
  // 1. Supplier Purchase Profile leadTimeDays
  // 2. Backend Purchasing configured default lead time (settings.defaultLeadTimeDays)
  // 3. Otherwise null (Expected Delivery remains empty/optional without arbitrary hardcoded business defaults)
  const configuredLeadTime = resolveConfiguredLeadTimeDays(
    supplierProfile?.leadTimeDays,
    settings?.defaultLeadTimeDays
  );

  useEffect(() => {
    if (!supplierPartyId || isDeliveryDateManual) return;
    if (configuredLeadTime !== null) {
      setExpectedDeliveryDate(addDaysToIsoDate(orderDate, configuredLeadTime));
    } else {
      setExpectedDeliveryDate("");
    }
  }, [supplierPartyId, configuredLeadTime, orderDate, isDeliveryDateManual]);

  const handleSupplierChange = (id: string) => {
    setSupplierPartyId(id);
    setIsDeliveryDateManual(false);
  };

  const handleDeliveryDateChange = (val: string) => {
    setIsDeliveryDateManual(true);
    setExpectedDeliveryDate(val);
  };

  const isDeliveryDateInvalid = Boolean(expectedDeliveryDate && orderDate && expectedDeliveryDate < orderDate);

  const [lines, setLines] = useState<DraftLine[]>([
    {
      id: "line-1",
      productVariantId: defaultProduct.variantId,
      description: defaultProduct.name,
      quantity: 10,
      unitCode: defaultProduct.uom,
      unitPrice: 120.0,
      isStocked: defaultProduct.isStocked
    }
  ]);

  const createMutation = useCreatePurchaseOrderMutation(legalEntityId);
  const approveAndBillMutation = useApproveAndBillMutation(legalEntityId, "temp");

  const handleAddLine = () => {
    setLines((prev) => [
      ...prev,
      {
        id: `line-${Date.now()}`,
        productVariantId: null,
        description: t("orders.serviceItemDefault"),
        quantity: 1,
        unitCode: "EA",
        unitPrice: 50.0,
        isStocked: false
      }
    ]);
  };

  const handleRemoveLine = (id: string) => {
    if (lines.length <= 1) return;
    setLines((prev) => prev.filter((l) => l.id !== id));
  };

  const handleLineChange = (id: string, updates: Partial<DraftLine>) => {
    setLines((prev) => prev.map((l) => (l.id === id ? { ...l, ...updates } : l)));
  };

  const subtotal = lines.reduce((sum, l) => sum + (l.quantity * (l.unitPrice || 0)), 0);
  const total = subtotal;

  const handleSubmit = async (action: "draft" | "approve" | "approveAndBill") => {
    setErrorMessage(null);

    if (!supplierPartyId) {
      setErrorMessage(t("errors.supplierRequired"));
      return;
    }

    if (isDeliveryDateInvalid) {
      setErrorMessage(t("errors.expectedDeliveryBeforeOrderDate"));
      return;
    }

    if (lines.length === 0 || lines.some((l) => l.quantity <= 0 || !l.description.trim())) {
      setErrorMessage(t("errors.linesInvalid"));
      return;
    }

    try {
      const orderLines: PurchaseOrderLineRequest[] = lines.map((l) => ({
        productVariantId: l.productVariantId || null,
        description: l.description.trim(),
        quantity: l.quantity,
        unitCode: l.unitCode || "EA",
        unitPrice: l.unitPrice,
        priceOverrideReason: l.priceOverrideReason || "Agreed Commercial Price"
      }));

      const createdId = await createMutation.mutateAsync({
        legalEntityId,
        supplierPartyId,
        currencyCode: currencyCode.trim().toUpperCase(),
        orderDate,
        approvalRequired: action !== "draft",
        lines: orderLines
      });

      if (action === "approveAndBill") {
        const idempotencyKey = generateIdempotencyKey("bill");
        const invoiceId = await approveAndBillMutation.mutateAsync({
          approval: {
            version: 1,
            idempotencyKey,
            reason: "Approve and bill immediate purchase"
          },
          invoice: {
            version: 1,
            idempotencyKey,
            documentDate: orderDate,
            postingDate: orderDate,
            taxDate: orderDate,
            supplierInvoiceNumber: `INV-SUP-${Math.floor(1000 + Math.random() * 9000)}`
          }
        });
        onClose();
        if (onCreated) onCreated(createdId, invoiceId);
      } else {
        onClose();
        if (onCreated) onCreated(createdId);
      }
    } catch (err) {
      const mapped = mapPurchasingError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={t("orders.newOrderTitle")}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", maxWidth: "48rem", width: "100%" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-light, #fef2f2)",
              color: "var(--color-status-danger, #dc2626)",
              border: "1px solid #fecaca",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {/* Top Header Fields */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(14rem, 1fr))", gap: "1rem" }}>
          <div>
            <PurchasingSupplierPicker
              value={supplierPartyId}
              onChange={handleSupplierChange}
              isRequired
            />
          </div>

          <div>
            <LocalDateField
              label={t("orders.orderDate")}
              value={orderDate}
              onChange={setOrderDate}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-primary)" }}>
              {t("orders.currency")}
            </label>
            <select
              value={currencyCode}
              onChange={(e) => setCurrencyCode(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                border: "1px solid var(--color-border-default)",
                borderRadius: "var(--radius-sm)",
                fontSize: "0.875rem",
                backgroundColor: "var(--color-surface-card)",
                color: "var(--color-content-primary)"
              }}
            >
              <option value="SAR">{t("suppliers.currencies.SAR")}</option>
              <option value="EGP">{t("suppliers.currencies.EGP")}</option>
              <option value="USD">{t("suppliers.currencies.USD")}</option>
            </select>
          </div>

          <div>
            <LocalDateField
              label={t("orders.expectedDeliveryDate")}
              value={expectedDeliveryDate}
              onChange={handleDeliveryDateChange}
            />
            {isDeliveryDateInvalid && (
              <span
                role="alert"
                style={{
                  fontSize: "0.75rem",
                  color: "var(--color-status-danger, #dc2626)",
                  marginTop: "0.25rem",
                  display: "block",
                  fontWeight: 600
                }}
              >
                {t("errors.expectedDeliveryBeforeOrderDate")}
              </span>
            )}
          </div>
        </div>

        {/* Lines Section */}
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("orders.lines")}
            </h3>
            <Button variant="secondary" onPress={handleAddLine} type="button">
              + {t("orders.addLine")}
            </Button>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            {lines.map((line, index) => (
              <div
                key={line.id}
                style={{
                  border: "1px solid var(--color-border-default)",
                  borderRadius: "var(--radius-md)",
                  padding: "1rem",
                  backgroundColor: "var(--color-surface-card)",
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.75rem"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                    #{index + 1}
                  </span>
                  {lines.length > 1 && (
                    <Button
                      variant="secondary"
                      onPress={() => handleRemoveLine(line.id)}
                      type="button"
                    >
                      {t("common.remove")}
                    </Button>
                  )}
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(12rem, 1fr))", gap: "0.75rem" }}>
                  <div>
                    <PurchasingProductPicker
                      value={line.productVariantId}
                      onChange={(variantId, product) => {
                        handleLineChange(line.id, {
                          productVariantId: variantId,
                          description: product.name,
                          unitCode: product.uom,
                          unitPrice: product.defaultBuyPrice,
                          isStocked: product.isStocked
                        });
                      }}
                    />
                  </div>

                  <div>
                    <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-muted)" }}>
                      {t("orders.lineDescription")} *
                    </label>
                    <input
                      type="text"
                      value={line.description}
                      onChange={(e) => handleLineChange(line.id, { description: e.target.value })}
                      placeholder={t("orders.lineDescriptionPlaceholder")}
                      style={{
                        width: "100%",
                        padding: "0.4375rem 0.625rem",
                        border: "1px solid var(--color-border-default)",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.875rem",
                        backgroundColor: "var(--color-surface-card)",
                        color: "var(--color-content-primary)"
                      }}
                    />
                  </div>

                  <div>
                    <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-muted)" }}>
                      {t("orders.quantity")} *
                    </label>
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={line.quantity}
                      onChange={(e) => handleLineChange(line.id, { quantity: Math.max(1, parseFloat(e.target.value) || 1) })}
                      style={{
                        width: "100%",
                        padding: "0.4375rem 0.625rem",
                        border: "1px solid var(--color-border-default)",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.875rem",
                        backgroundColor: "var(--color-surface-card)",
                        color: "var(--color-content-primary)"
                      }}
                    />
                  </div>

                  <div>
                    <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, marginBottom: "0.25rem", color: "var(--color-content-muted)" }}>
                      {t("orders.buyPrice")} ({currencyCode}) *
                    </label>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      disabled={!hasPriceOverridePermission}
                      value={line.unitPrice}
                      onChange={(e) => handleLineChange(line.id, { unitPrice: Math.max(0, parseFloat(e.target.value) || 0) })}
                      style={{
                        width: "100%",
                        padding: "0.4375rem 0.625rem",
                        border: "1px solid var(--color-border-default)",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.875rem",
                        backgroundColor: hasPriceOverridePermission ? "var(--color-surface-card)" : "var(--color-surface-subtle)",
                        color: "var(--color-content-primary)"
                      }}
                    />
                  </div>

                  <div style={{ display: "flex", flexDirection: "column", justifyContent: "flex-end" }}>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
                      {t("orders.lineTotal")}
                    </span>
                    <span style={{ fontSize: "0.9375rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                      <MoneyDisplay value={toMoneyVal(line.quantity * line.unitPrice, currencyCode)} />
                    </span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Pricing & Commercial Commitment Summary */}
        <div
          style={{
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-md)",
            padding: "1rem 1.25rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem"
          }}
        >
          <div>
            <span style={{ fontSize: "0.8125rem", fontWeight: 600, color: "var(--color-content-muted)" }}>
              {t("orders.commercialCommitment")}
            </span>
            <p style={{ margin: "0.125rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("orders.pricingPreviewNotice")}
            </p>
          </div>
          <div style={{ textAlign: "right" }}>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
              <MoneyDisplay value={toMoneyVal(total, currencyCode)} />
            </span>
          </div>
        </div>

        {/* Action Buttons */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", flexWrap: "wrap", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="secondary" onPress={() => handleSubmit("draft")} isDisabled={isDeliveryDateInvalid} type="button">
            {t("orders.saveDraft")}
          </Button>
          <Button variant="primary" onPress={() => handleSubmit("approve")} isDisabled={isDeliveryDateInvalid} type="button">
            {t("orders.approve")}
          </Button>
          <Button variant="primary" onPress={() => handleSubmit("approveAndBill")} isDisabled={isDeliveryDateInvalid} type="button">
            {t("orders.approveAndBill")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
