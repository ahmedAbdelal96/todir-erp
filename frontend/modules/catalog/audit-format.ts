import { i18n, currentLanguage } from "../../core/i18n/i18n.ts";
import type { en as catalogResource } from "./i18n/en.ts";

type NestedKey<T> = { [Key in keyof T & string]: T[Key] extends string ? Key : `${Key}.${NestedKey<T[Key]>}` }[keyof T & string];

function translateCatalog(key: NestedKey<typeof catalogResource>): string {
  return i18n.getFixedT(currentLanguage(), "catalog")(key);
}

export interface CatalogAuditChange {
  readonly label: string;
  readonly beforeValue?: string;
  readonly afterValue?: string;
}

export interface CatalogAuditEventDisplay {
  readonly title: string;
  readonly description?: string;
  readonly changes: readonly CatalogAuditChange[];
}

export function formatCatalogAuditEvent(action: string, detailJson?: string | null): CatalogAuditEventDisplay {
  let parsed: Record<string, unknown> | null = null;
  if (detailJson) {
    try {
      parsed = JSON.parse(detailJson);
    } catch {
      parsed = null;
    }
  }

  const actionTitles: Record<string, NestedKey<typeof catalogResource>> = {
    product_created: "audit.events.created",
    product_identity_changed: "audit.events.updated",
    product_archived: "audit.events.archived",
    product_restored: "audit.events.restored",
    product_visibility_changed: "audit.events.visibilityChanged",
    product_barcode_added: "audit.events.barcodeAdded",
    product_variant_created: "audit.events.variantCreated",
    product_uom_changed: "audit.events.uomChanged",
    product_reference_added: "audit.events.referenceAdded"
  };

  const titleKey = actionTitles[action] ?? "audit.events.generalChange";
  const title = translateCatalog(titleKey);

  const changes: CatalogAuditChange[] = [];

  if (parsed && typeof parsed === "object") {
    if ("DisplayName" in parsed || "displayName" in parsed) {
      const val = String(parsed.DisplayName ?? parsed.displayName ?? "");
      changes.push({
        label: translateCatalog("audit.fields.displayName"),
        afterValue: val
      });
    }
    if ("Nature" in parsed || "nature" in parsed) {
      const nature = String(parsed.Nature ?? parsed.nature ?? "");
      changes.push({
        label: translateCatalog("audit.fields.nature"),
        afterValue: nature === "Service" ? translateCatalog("common.service") : translateCatalog("common.goods")
      });
    }
    if ("Sku" in parsed || "sku" in parsed) {
      changes.push({
        label: translateCatalog("audit.fields.sku"),
        afterValue: String(parsed.Sku ?? parsed.sku ?? "")
      });
    }
    if ("Value" in parsed || "value" in parsed) {
      changes.push({
        label: translateCatalog("audit.fields.barcode"),
        afterValue: String(parsed.Value ?? parsed.value ?? "")
      });
    }
    if ("ScopeType" in parsed || "scopeType" in parsed) {
      const scope = String(parsed.ScopeType ?? parsed.scopeType ?? "");
      changes.push({
        label: translateCatalog("audit.fields.scopeType"),
        afterValue: scope === "TenantWide" ? translateCatalog("audit.values.tenantWide") : translateCatalog("audit.values.legalEntity")
      });
    }
    if ("FactorToBase" in parsed || "factorToBase" in parsed) {
      changes.push({
        label: translateCatalog("audit.fields.factorToBase"),
        afterValue: String(parsed.FactorToBase ?? parsed.factorToBase ?? "")
      });
    }
  }

  return { title, changes };
}
