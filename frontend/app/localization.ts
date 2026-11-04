import { initializeLocalization } from "../core/i18n/i18n.ts";
import { ar as accountingAr } from "../modules/accounting/i18n/ar.ts";
import { en as accountingEn } from "../modules/accounting/i18n/en.ts";
import { ar as catalogAr } from "../modules/catalog/i18n/ar.ts";
import { en as catalogEn } from "../modules/catalog/i18n/en.ts";
import { ar as directoryAr } from "../modules/directory/i18n/ar.ts";
import { en as directoryEn } from "../modules/directory/i18n/en.ts";
import { ar as finbaseAr } from "../modules/finbase/i18n/ar.ts";
import { en as finbaseEn } from "../modules/finbase/i18n/en.ts";
import { ar as identityAr } from "../modules/identity/i18n/ar.ts";
import { en as identityEn } from "../modules/identity/i18n/en.ts";
import { ar as invoicingAr } from "../modules/invoicing/i18n/ar.ts";
import { en as invoicingEn } from "../modules/invoicing/i18n/en.ts";
import { ar as migrationAr } from "../modules/migration/i18n/ar.ts";
import { en as migrationEn } from "../modules/migration/i18n/en.ts";
import { ar as paymentsAr } from "../modules/payments/i18n/ar.ts";
import { en as paymentsEn } from "../modules/payments/i18n/en.ts";
import { ar as pricingAr } from "../modules/pricing/i18n/ar.ts";
import { en as pricingEn } from "../modules/pricing/i18n/en.ts";
import { ar as taxAr } from "../modules/tax/i18n/ar.ts";
import { en as taxEn } from "../modules/tax/i18n/en.ts";
import { ar as subledgerAr } from "../modules/subledger/i18n/ar.ts";
import { en as subledgerEn } from "../modules/subledger/i18n/en.ts";
import { ar as inventoryAr } from "../modules/inventory/i18n/ar.ts";
import { en as inventoryEn } from "../modules/inventory/i18n/en.ts";
import { ar as salesAr } from "../modules/sales/i18n/ar.ts";
import { en as salesEn } from "../modules/sales/i18n/en.ts";
import { ar as purchasingAr } from "../modules/purchasing/i18n/ar.ts";
import { en as purchasingEn } from "../modules/purchasing/i18n/en.ts";
import { ar as hrAr } from "../modules/hr/i18n/ar.ts";
import { en as hrEn } from "../modules/hr/i18n/en.ts";
import { payrollAr } from "../modules/payroll/i18n/ar.ts";
import { payrollEn } from "../modules/payroll/i18n/en.ts";

const resources = {
  ar: {
    accounting: accountingAr,
    catalog: catalogAr,
    directory: directoryAr,
    finbase: finbaseAr,
    identity: identityAr,
    invoicing: invoicingAr,
    migration: migrationAr,
    payments: paymentsAr,
    pricing: pricingAr,
    tax: taxAr,
    subledger: subledgerAr,
    inventory: inventoryAr,
    sales: salesAr,
    purchasing: purchasingAr,
    hr: hrAr,
    payroll: payrollAr
  },
  en: {
    accounting: accountingEn,
    catalog: catalogEn,
    directory: directoryEn,
    finbase: finbaseEn,
    identity: identityEn,
    invoicing: invoicingEn,
    migration: migrationEn,
    payments: paymentsEn,
    pricing: pricingEn,
    tax: taxEn,
    subledger: subledgerEn,
    inventory: inventoryEn,
    sales: salesEn,
    purchasing: purchasingEn,
    hr: hrEn,
    payroll: payrollEn
  }
} as const;

export const i18nReady = initializeLocalization(resources);

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "common";
    resources: typeof resources["en"] & {
      common: typeof import("../core/i18n/resources/en/common.ts").en;
      navigation: typeof import("../core/i18n/resources/en/navigation.ts").en;
      validation: typeof import("../core/i18n/resources/en/validation.ts").en;
      workspace: typeof import("../core/i18n/resources/en/workspace.ts").en;
    };
  }
}
