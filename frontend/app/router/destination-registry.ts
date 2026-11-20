import type { NavigationCategory, NavDestination, ModuleNavigationRegistration } from "./route-contract.ts";
import type { EffectiveAccess } from "../../core/runtime/effective-access.ts";
import { evaluatePermissionCondition, isModuleOperational } from "../../core/runtime/effective-access.ts";

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
    keywords: ["إدارة", "فريق", "تقارير", "management", "teams"]
  }
];

export function isDestinationPermitted(
  dest: NavDestination,
  access: EffectiveAccess | null | undefined
): boolean {
  if (!access) return false;
  if (!isModuleOperational(access, dest.moduleId)) return false;
  return evaluatePermissionCondition(dest.permission, access);
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
  const targetModuleId = item.moduleIds[0] ?? item.id;
  if (!isModuleOperational(access, targetModuleId)) return false;
  return evaluatePermissionCondition(item.permissions ?? item.permission, access);
}

export function getPermittedDestinationsForModule(
  item: ModuleNavigationRegistration,
  access: EffectiveAccess | null | undefined
): readonly NavDestination[] {
  if (!item.destinations || item.destinations.length === 0) return [];
  if (!access) return [];
  return item.destinations.filter(d => isDestinationPermitted(d, access));
}
