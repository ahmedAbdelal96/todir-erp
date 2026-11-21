import type { NavigationCategory, NavDestination, ModuleNavigationRegistration } from "./route-contract.ts";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import {
  evaluatePermissionCondition,
  evaluateEffectivePermissionCondition,
  isModuleOperational,
  isModuleReadOnly
} from "../../core/runtime/effective-access.ts";

export interface NavigationCategoryGroup {
  readonly id: NavigationCategory;
  readonly labelKey: string;
  readonly fallbackLabel: string;
  readonly icon: string;
  readonly order: number;
}

export const NAVIGATION_CATEGORIES: readonly NavigationCategoryGroup[] = [
  { id: "workspace", labelKey: "categories.workspace", fallbackLabel: "الرئيسية", icon: "home", order: 10 },
  { id: "sales", labelKey: "categories.sales", fallbackLabel: "المبيعات والعملاء", icon: "sales", order: 20 },
  { id: "purchasing", labelKey: "categories.purchasing", fallbackLabel: "المشتريات والموردون", icon: "purchasing", order: 30 },
  { id: "inventory", labelKey: "categories.inventory", fallbackLabel: "المخزون والتشغيل", icon: "inventory", order: 40 },
  { id: "finance", labelKey: "categories.finance", fallbackLabel: "المالية والخزائن", icon: "banking", order: 50 },
  { id: "hr", labelKey: "categories.hr", fallbackLabel: "الموارد البشرية", icon: "hr", order: 60 },
  { id: "admin", labelKey: "categories.admin", fallbackLabel: "الإدارة والإعدادات", icon: "settings", order: 70 },
];

export const BANKING_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "banking-overview",
    category: "finance",
    moduleId: "banking",
    path: "/banking",
    labelKey: "tabs.overview",
    fallbackLabel: "نظرة عامة على السيولة",
    icon: "landmark",
    permission: { anyOf: ["banking.accounts.read", "banking.statements.read", "banking.reconciliation.read", "banking.reports.read", "banking.cash_position.read"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["بنوك", "سيولة", "خزائن", "overview", "liquidity"]
  },
  {
    id: "banking-accounts",
    category: "finance",
    moduleId: "banking",
    path: "/banking/accounts",
    labelKey: "tabs.accounts",
    fallbackLabel: "الحسابات البنكية والخزائن",
    icon: "landmark",
    permission: { anyOf: ["banking.accounts.read", "banking.accounts.create", "banking.accounts.edit"] },
    order: 20,
    mode: "mixed",
    keywords: ["حسابات", "بنك", "خزينة", "صندوق", "accounts", "treasury"]
  },
  {
    id: "banking-statements",
    category: "finance",
    moduleId: "banking",
    path: "/banking/statements",
    labelKey: "tabs.statements",
    fallbackLabel: "كشوف الحسابات البنكية",
    icon: "statement",
    permission: { anyOf: ["banking.statements.read", "banking.statements.import", "banking.statements.validate"] },
    order: 30,
    mode: "mixed",
    keywords: ["كشف", "حساب", "إفادة", "statements", "feed"]
  },
  {
    id: "banking-reconciliations",
    category: "finance",
    moduleId: "banking",
    path: "/banking/reconciliations",
    labelKey: "tabs.reconciliation",
    fallbackLabel: "مطابقة كشف الحساب البنكي",
    icon: "reconcile",
    permission: { anyOf: ["banking.reconciliation.read", "banking.reconciliation.match", "banking.reconciliation.finalize"] },
    order: 40,
    mode: "mixed",
    keywords: ["تسوية", "مطابقة", "reconciliation", "match"]
  },
  {
    id: "banking-cash",
    category: "finance",
    moduleId: "banking",
    path: "/banking/cash",
    labelKey: "tabs.cash",
    fallbackLabel: "جرد النقدية والعهد",
    icon: "vault",
    permission: { anyOf: ["banking.cash.read", "banking.cash.count", "banking.cash.difference.resolve"] },
    order: 50,
    mode: "mixed",
    keywords: ["جرد", "نقدية", "عهدة", "خزنة", "cash count", "custody"]
  },
  {
    id: "banking-transfers",
    category: "finance",
    moduleId: "banking",
    path: "/banking/transfers",
    labelKey: "tabs.transfers",
    fallbackLabel: "التحويلات وتسويات بوابات الدفع",
    icon: "transfer",
    permission: { anyOf: ["banking.transfers.read", "banking.transfers.initiate"] },
    order: 60,
    mode: "mixed",
    keywords: ["تحويل", "تسوية", "بوابة", "transfers", "payouts"]
  },
  {
    id: "banking-forecast",
    category: "finance",
    moduleId: "banking",
    path: "/banking/forecast",
    labelKey: "tabs.forecast",
    fallbackLabel: "توقعات السيولة النقدية",
    icon: "forecast",
    permission: { anyOf: ["banking.cash_forecast.read", "banking.cash_forecast.manage"] },
    order: 70,
    mode: "read",
    keywords: ["توقعات", "سيولة", "تدفق", "forecast", "cash flow"]
  },
  {
    id: "banking-reports",
    category: "finance",
    moduleId: "banking",
    path: "/banking/reports",
    labelKey: "tabs.reports",
    fallbackLabel: "تقارير حركة النقدية والمطابقة",
    icon: "reports",
    permission: { anyOf: ["banking.reports.read", "banking.audit.read"] },
    order: 80,
    mode: "read",
    keywords: ["تقارير", "كشف حركة", "reports"]
  }
];

export const CRM_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "crm-home",
    category: "sales",
    moduleId: "crm",
    path: "/crm",
    labelKey: "tabs.home",
    fallbackLabel: "مبيعاتي اليوم",
    icon: "calendar-check",
    permission: { anyOf: ["crm.home.read", "crm.lead.read", "crm.opportunity.read", "crm.activity.read"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["مبيعات", "مهام", "اليوم", "my work"]
  },
  {
    id: "crm-leads",
    category: "sales",
    moduleId: "crm",
    path: "/crm/leads",
    labelKey: "tabs.leads",
    fallbackLabel: "العملاء المحتملين",
    icon: "users",
    permission: { anyOf: ["crm.lead.read", "crm.lead.create", "crm.lead.edit"] },
    order: 20,
    mode: "mixed",
    keywords: ["عميل محتمل", "عملاء", "leads"]
  },
  {
    id: "crm-opportunities",
    category: "sales",
    moduleId: "crm",
    path: "/crm/opportunities",
    labelKey: "tabs.opportunities",
    fallbackLabel: "الفرص والمسار البيعي",
    icon: "kanban",
    permission: { anyOf: ["crm.opportunity.read", "crm.opportunity.create", "crm.opportunity.edit"] },
    order: 30,
    mode: "mixed",
    keywords: ["فرص", "صفقات", "مسار بيعي", "pipeline", "deals"]
  },
  {
    id: "crm-activities",
    category: "sales",
    moduleId: "crm",
    path: "/crm/activities",
    labelKey: "tabs.activities",
    fallbackLabel: "الأنشطة والمتابعات",
    icon: "check-square",
    permission: { anyOf: ["crm.activity.read", "crm.activity.create", "crm.activity.edit"] },
    order: 40,
    mode: "mixed",
    keywords: ["أنشطة", "متابعات", "مكالمات", "activities", "tasks"]
  },
  {
    id: "crm-management",
    category: "sales",
    moduleId: "crm",
    path: "/crm/management",
    labelKey: "tabs.management",
    fallbackLabel: "إدارة المبيعات والتقارير",
    icon: "bar-chart",
    permission: { anyOf: ["crm.team.manage", "crm.pipeline.manage", "crm.team.read", "crm.pipeline.read", "crm.forecast.read"] },
    order: 50,
    mode: "mixed",
    keywords: ["إدارة", "فريق", "تقارير", "management", "teams"]
  }
];

export const ACCOUNTING_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "accounting-overview",
    category: "finance",
    moduleId: "ledger",
    path: "/accounting",
    labelKey: "tabs.chartOfAccounts",
    fallbackLabel: "دليل الحسابات والأرصدة",
    icon: "landmark",
    permission: { anyOf: ["ledger.account.read", "ledger.account.manage"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["حسابات", "دليل", "أرصدة", "شجرة الحسابات", "chart of accounts", "coa"]
  },
  {
    id: "accounting-journals",
    category: "finance",
    moduleId: "ledger",
    path: "/accounting/journals",
    labelKey: "tabs.journals",
    fallbackLabel: "قيود اليومية والترحيل",
    icon: "statement",
    permission: { anyOf: ["ledger.journal.read", "ledger.journal.manage", "ledger.general_journal.post"] },
    order: 20,
    mode: "mixed",
    keywords: ["قيد", "يومية", "ترحيل", "سند قيد", "journals", "entries"]
  },
  {
    id: "accounting-ledger",
    category: "finance",
    moduleId: "ledger",
    path: "/accounting/ledger",
    labelKey: "tabs.generalLedger",
    fallbackLabel: "دفتر الأستاذ العام",
    icon: "reports",
    permission: "ledger.reports.read",
    order: 30,
    mode: "read",
    keywords: ["أستاذ", "دفتر الأستاذ", "كشف حساب", "general ledger", "gl"]
  },
  {
    id: "accounting-trial-balance",
    category: "finance",
    moduleId: "ledger",
    path: "/accounting/trial-balance",
    labelKey: "tabs.trialBalance",
    fallbackLabel: "ميزان المراجعة",
    icon: "reports",
    permission: "ledger.reports.read",
    order: 40,
    mode: "read",
    keywords: ["ميزان", "مراجعة", "أرصدة", "trial balance", "tb"]
  },
  {
    id: "accounting-statements",
    category: "finance",
    moduleId: "accounting",
    path: "/accounting/statements",
    labelKey: "tabs.financialStatements",
    fallbackLabel: "القوائم المالية والختامية",
    icon: "forecast",
    permission: { anyOf: ["accounting.statement.read", "accounting.statement.manage"] },
    order: 50,
    mode: "mixed",
    keywords: ["قوائم مالية", "ميزانية", "أرباح وخسائر", "تدفقات", "financial statements", "balance sheet", "p&l"]
  },
  {
    id: "accounting-periods",
    category: "finance",
    moduleId: "ledger",
    path: "/accounting/periods",
    labelKey: "tabs.periods",
    fallbackLabel: "الفترات المالية والإقفال",
    icon: "vault",
    permission: { anyOf: ["ledger.period.read", "ledger.period.manage", "accounting.close.execute"] },
    order: 60,
    mode: "mixed",
    keywords: ["فترة مالية", "سنة مالية", "إقفال", "periods", "fiscal year", "close"]
  },
  {
    id: "accounting-reconciliation",
    category: "finance",
    moduleId: "accounting",
    path: "/accounting/reconciliation",
    labelKey: "tabs.controlReconciliation",
    fallbackLabel: "مطابقة حسابات المراقبة",
    icon: "reconcile",
    permission: { anyOf: ["accounting.reconciliation.read", "accounting.reconciliation.run"] },
    order: 70,
    mode: "mixed",
    keywords: ["مطابقة", "مراقبة", "subledger", "reconciliation"]
  },
  {
    id: "accounting-revaluation",
    category: "finance",
    moduleId: "accounting",
    path: "/accounting/revaluation",
    labelKey: "tabs.fxRevaluation",
    fallbackLabel: "تقييم فروق العملة",
    icon: "transfer",
    permission: { anyOf: ["accounting.fx-revaluation.read", "accounting.fx-revaluation.post"] },
    order: 80,
    mode: "mixed",
    keywords: ["عملات", "فروق عملة", "تقييم", "fx", "revaluation"]
  }
];

export const INVENTORY_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "inventory-overview",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory",
    labelKey: "tabs.overview",
    fallbackLabel: "أرصدة ومواقع المخزون",
    icon: "inventory",
    permission: "inventory.stock.read",
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["مخزون", "أرصدة", "بضاعة", "مستودع", "stock", "balances", "inventory"]
  },
  {
    id: "inventory-movements",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/movements",
    labelKey: "tabs.history",
    fallbackLabel: "سجل حركة الأصناف المخزنية",
    icon: "statement",
    permission: "inventory.history.read",
    order: 20,
    mode: "read",
    keywords: ["حركة", "وارد", "منصرف", "سجل", "movements", "transactions"]
  },
  {
    id: "inventory-transfers",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/transfers",
    labelKey: "tabs.transfers",
    fallbackLabel: "أوامر التحويل بين المستودعات",
    icon: "transfer",
    permission: { anyOf: ["inventory.transfer.create", "inventory.transfer.ship", "inventory.transfer.receive"] },
    order: 30,
    mode: "mixed",
    keywords: ["تحويل مخزني", "نقل بضاعة", "مستودعات", "transfers"]
  },
  {
    id: "inventory-counts",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/counts",
    labelKey: "tabs.counts",
    fallbackLabel: "محاضر الجرد الفعلي للمخزون",
    icon: "vault",
    permission: { anyOf: ["inventory.count.create", "inventory.count.enter", "inventory.count.approve", "inventory.count.post"] },
    order: 40,
    mode: "mixed",
    keywords: ["جرد", "جرد دوري", "مطابقة فعلية", "stock count", "physical inventory"]
  },
  {
    id: "inventory-adjustments",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/adjustments",
    labelKey: "tabs.adjustments",
    fallbackLabel: "أذون التسوية المخزنية",
    icon: "reconcile",
    permission: { anyOf: ["inventory.adjustment.create", "inventory.adjustment.approve", "inventory.adjustment.post"] },
    order: 50,
    mode: "mixed",
    keywords: ["تسوية", "تعديل كميات", "عجز", "زيادة", "adjustments"]
  },
  {
    id: "inventory-warehouses",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/warehouses",
    labelKey: "tabs.warehouses",
    fallbackLabel: "المستودعات ومواقع التخزين",
    icon: "landmark",
    permission: { anyOf: ["inventory.warehouse.manage", "inventory.location.manage"] },
    order: 60,
    mode: "mixed",
    keywords: ["مستودع", "موقع تخزين", "مخازن", "رفوف", "warehouses", "locations"]
  },
  {
    id: "inventory-traceability",
    category: "inventory",
    moduleId: "inventory",
    path: "/inventory/traceability",
    labelKey: "tabs.traceability",
    fallbackLabel: "تتبع التشغيلات والأرقام التسلسلية",
    icon: "reports",
    permission: "inventory.traceability.manage",
    order: 70,
    mode: "read",
    keywords: ["تشغيلة", "رقم تسلسلي", "صلاحية", "batch", "lot", "serial", "traceability"]
  }
];

export function isDestinationPermitted(
  dest: NavDestination,
  access: EffectiveAccess | null | undefined
): boolean {
  if (!access) return false;
  if (!isModuleOperational(access, dest.moduleId, true)) return false;

  const isReadOnly = isModuleReadOnly(access, dest.moduleId);
  if (isReadOnly && dest.mode === "mutation") {
    return false;
  }

  return evaluateEffectivePermissionCondition(dest.permission, access, true);
}

export function isModulePermitted(
  item: ModuleNavigationRegistration,
  access: EffectiveAccess | null | undefined
): boolean {
  if (!access) return false;
  // If module defines task destinations, visible if AT LEAST ONE destination is permitted
  if (item.destinations && item.destinations.length > 0) {
    return item.destinations.some(d => isDestinationPermitted(d, access));
  }
  const moduleIds = item.moduleIds && item.moduleIds.length > 0 ? item.moduleIds : [item.id];
  if (!moduleIds.some(m => isModuleOperational(access, m, true))) return false;
  return evaluateEffectivePermissionCondition(item.permissions ?? item.permission, access, true);
}

export function getPermittedDestinationsForModule(
  item: ModuleNavigationRegistration,
  access: EffectiveAccess | null | undefined
): readonly NavDestination[] {
  if (!item.destinations || item.destinations.length === 0) return [];
  if (!access) return [];
  return item.destinations.filter(d => isDestinationPermitted(d, access));
}

export function resolveModuleLandingPath(
  item: ModuleNavigationRegistration,
  access: EffectiveAccess | null | undefined
): string {
  if (!item.destinations || item.destinations.length === 0) return item.path;
  if (!access) return item.path;
  const permitted = getPermittedDestinationsForModule(item, access);
  if (permitted.length === 0) return item.path;
  const matchesRoot = permitted.some(d => d.path === item.path);
  if (matchesRoot) return item.path;
  return permitted[0]?.path ?? item.path;
}
