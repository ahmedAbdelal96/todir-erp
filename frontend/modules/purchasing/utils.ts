import type { QuantityValue, MoneyValue } from "../../components/contracts/domain-values.ts";
import { resolvePurchasingProduct } from "./components/purchasing-product-resolver.ts";

export function toMoneyVal(amount: number | string | undefined | null, currency = "SAR"): MoneyValue {
  if (amount == null) return { amount: "0.00", currency };
  const num = typeof amount === "number" ? amount : parseFloat(String(amount));
  if (isNaN(num)) return { amount: "0.00", currency };
  return {
    amount: num.toFixed(2),
    currency
  };
}

export function toQty(quantity: number | string | undefined | null, unitOfMeasure = "EA"): QuantityValue {
  if (quantity == null) return { value: "0", unitOfMeasure };
  const num = typeof quantity === "number" ? quantity : parseFloat(String(quantity));
  if (isNaN(num)) return { value: "0", unitOfMeasure };
  return {
    value: Number.isInteger(num) ? num.toString() : num.toFixed(2),
    unitOfMeasure
  };
}

export function generateIdempotencyKey(prefix = "purchasing"): string {
  const ts = Date.now().toString(36);
  const rnd = Math.random().toString(36).substring(2, 8);
  return `${prefix}-${ts}-${rnd}`;
}

export function formatPoNumber(id: string, sequenceNumber?: number): string {
  if (sequenceNumber != null) {
    return `PO-2026-${String(sequenceNumber).padStart(4, "0")}`;
  }
  const cleanId = id.replace(/-/g, "").toUpperCase();
  const shortCode = cleanId.substring(cleanId.length - 4);
  return `PO-2026-${shortCode}`;
}

export function addDaysToIsoDate(isoDate: string, days: number): string {
  if (!isoDate) return "";
  const parts = isoDate.split("-").map(Number);
  if (parts.length !== 3 || parts.some(isNaN)) return "";
  const year = parts[0];
  const month = parts[1];
  const day = parts[2];
  if (year === undefined || month === undefined || day === undefined) return "";
  const d = new Date(Date.UTC(year, month - 1, day));
  d.setUTCDate(d.getUTCDate() + days);
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, "0");
  const dd = String(d.getUTCDate()).padStart(2, "0");
  return `${y}-${m}-${dd}`;
}

export interface ReceiptEligibilityCandidate {
  readonly productVariantId?: string | null | undefined;
  readonly receiptEligible?: boolean | null | undefined;
  readonly isInventoryTracked?: boolean | null | undefined;
  readonly isStocked?: boolean | null | undefined;
  readonly requiresInventoryMovement?: boolean | null | undefined;
  readonly stockBehavior?: string | null | undefined;
  readonly inventoryTrackingMode?: string | null | undefined;
  readonly lineType?: string | null | undefined;
  readonly productType?: string | null | undefined;
}

/**
 * Authoritative Goods Receipt Eligibility Invariant:
 * ProductVariantId existence != Inventory Tracking.
 * A line is physical receipt eligible ONLY when backed by authoritative Catalog/Inventory
 * classification (StockTracked). Non-stock goods, services (even with a valid ProductVariantId),
 * and ad-hoc lines must NEVER be received into inventory.
 */
export function isReceiptEligibleLine(line?: ReceiptEligibilityCandidate | null): boolean {
  if (!line) return false;

  // 1. Authoritative explicit boolean flags on line contract take precedence
  if (typeof line.receiptEligible === "boolean") return line.receiptEligible;
  if (typeof line.isInventoryTracked === "boolean") return line.isInventoryTracked;
  if (typeof line.requiresInventoryMovement === "boolean") return line.requiresInventoryMovement;
  if (typeof line.isStocked === "boolean") return line.isStocked;

  // 2. Authoritative string enums / classifications on line
  if (line.stockBehavior) {
    const sb = line.stockBehavior.toLowerCase();
    return sb === "stocktracked" || sb === "stocked";
  }
  if (line.inventoryTrackingMode) {
    const tm = line.inventoryTrackingMode.toLowerCase();
    return tm !== "none" && tm !== "nonstocked";
  }
  if (line.lineType) {
    const lt = line.lineType.toLowerCase();
    return lt === "stocked" || lt === "stocktracked";
  }
  if (line.productType) {
    return line.productType.toLowerCase() === "goods";
  }

  // 3. Authoritative Catalog Product resolution if a variant ID is present
  if (line.productVariantId) {
    const product = resolvePurchasingProduct(line.productVariantId);
    return Boolean(product.isStocked);
  }

  // 4. Ad-hoc service/expense lines without productVariantId or classification
  return false;
}

/**
 * Authoritative configured lead time resolver:
 * Default priority:
 * 1. Supplier Purchase Profile leadTimeDays
 * 2. Backend / Purchasing configured default lead time (settings.defaultLeadTimeDays)
 * 3. otherwise null (leave empty / optional).
 * React must NOT own 7 days or any arbitrary default.
 */
export function resolveConfiguredLeadTimeDays(
  supplierLeadTime?: number | null | undefined,
  settingsDefaultLeadTime?: number | null | undefined
): number | null {
  if (typeof supplierLeadTime === "number" && !isNaN(supplierLeadTime) && supplierLeadTime >= 0) {
    return supplierLeadTime;
  }
  if (typeof settingsDefaultLeadTime === "number" && !isNaN(settingsDefaultLeadTime) && settingsDefaultLeadTime >= 0) {
    return settingsDefaultLeadTime;
  }
  return null;
}
