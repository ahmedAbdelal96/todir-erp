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
    mode: "mutation",
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

export const MANUFACTURING_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "manufacturing-overview",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing",
    labelKey: "tabs.overview",
    fallbackLabel: "لوحة التحكم وأولويات العمل",
    icon: "dashboard",
    permission: { anyOf: ["manufacturing.read", "manufacturing.production.read", "manufacturing.mrp.read", "manufacturing.execution.manage"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["تصنيع", "لوحة التحكم", "أولويات", "نظرة عامة", "manufacturing", "overview"]
  },
  {
    id: "manufacturing-orders",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/orders",
    labelKey: "tabs.orders",
    fallbackLabel: "أوامر الإنتاج والتصنيع",
    icon: "assignment",
    permission: { anyOf: ["manufacturing.production.read", "manufacturing.production.manage", "manufacturing.production.create", "manufacturing.production.release"] },
    order: 20,
    mode: "mixed",
    keywords: ["أمر تصنيع", "أوامر الإنتاج", "تشغيل", "production orders", "work orders"]
  },
  {
    id: "manufacturing-shop-floor",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/shop-floor",
    labelKey: "tabs.shopFloor",
    fallbackLabel: "صالة التشغيل وأرضية المصنع",
    icon: "precision_manufacturing",
    permission: { anyOf: ["manufacturing.execution.manage", "manufacturing.production.execute"] },
    order: 30,
    mode: "mixed",
    keywords: ["صالة التشغيل", "طابور العمليات", "مشغل", "ماكينة", "shop floor", "execution"]
  },
  {
    id: "manufacturing-materials",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/materials",
    labelKey: "tabs.materials",
    fallbackLabel: "جاهزية وصرف المواد الخام",
    icon: "category",
    permission: { anyOf: ["manufacturing.material.read", "manufacturing.material.issue", "manufacturing.material.consume", "manufacturing.material.return", "manufacturing.execution.manage"] },
    order: 40,
    mode: "mixed",
    keywords: ["صرف خامات", "حجز مواد", "استهلاك", "ارتجاع", "materials", "issuance"]
  },
  {
    id: "manufacturing-mrp",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/mrp",
    labelKey: "tabs.mrp",
    fallbackLabel: "تخطيط المتطلبات والإنتاج (MRP)",
    icon: "schedule",
    permission: { anyOf: ["manufacturing.mrp.read", "manufacturing.mrp.manage", "manufacturing.mrp.run", "manufacturing.planning.read", "manufacturing.planning.run", "manufacturing.planning.firm"] },
    order: 50,
    mode: "mixed",
    keywords: ["تخطيط", "mrp", "mps", "توقعات", "عجز خامات", "planning", "forecast"]
  },
  {
    id: "manufacturing-definitions",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/definitions",
    labelKey: "tabs.definitions",
    fallbackLabel: "البيانات الأساسية وقوائم المواد (BOM)",
    icon: "schema",
    permission: { anyOf: ["manufacturing.definition.read", "manufacturing.definition.manage", "manufacturing.plant.manage", "manufacturing.bom.read", "manufacturing.bom.create", "manufacturing.routing.read", "manufacturing.capacity.manage"] },
    order: 60,
    mode: "mixed",
    keywords: ["قائمة مواد", "bom", "وصفة", "مسار عمليات", "مصانع", "مراكز عمل", "definitions", "routing"]
  },
  {
    id: "manufacturing-capacity",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/capacity",
    labelKey: "tabs.capacity",
    fallbackLabel: "الطاقة الإنتاجية ومراكز العمل",
    icon: "event_available",
    permission: { anyOf: ["manufacturing.capacity.manage", "manufacturing.read"] },
    order: 70,
    mode: "mixed",
    keywords: ["طاقة", "ساعات تشغيل", "تقويم", "توقف وصيانة", "capacity", "work centers"]
  },
  {
    id: "manufacturing-genealogy",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/genealogy",
    labelKey: "tabs.genealogy",
    fallbackLabel: "شجرة تتبع التشغيلات (Genealogy)",
    icon: "account_tree",
    permission: { anyOf: ["manufacturing.production.read", "manufacturing.read"] },
    order: 80,
    mode: "read",
    keywords: ["تتبع", "تشغيلة", "شجرة", "genealogy", "traceability", "lot"]
  },
  {
    id: "manufacturing-costing",
    category: "inventory",
    moduleId: "manufacturing",
    path: "/manufacturing/costing",
    labelKey: "tabs.costing",
    fallbackLabel: "تكاليف الإنتاج وإقفال الـ WIP",
    icon: "payments",
    permission: { anyOf: ["manufacturing.cost.read", "manufacturing.cost.manage", "manufacturing.cost.sensitive.read"] },
    order: 90,
    mode: "mixed",
    keywords: ["تكاليف تصنيع", "فروق التكاليف", "تحت التشغيل", "wip", "costing", "variance"]
  }
];

export const SALES_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "sales-overview",
    category: "sales",
    moduleId: "sales",
    path: "/sales",
    labelKey: "tabs.overview",
    fallbackLabel: "نظرة عامة على المبيعات",
    icon: "sales",
    permission: { anyOf: ["sales.quotation.read", "sales.order.read", "sales.history.read"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["مبيعات", "أداء", "ملخص", "sales", "overview", "revenue"]
  },
  {
    id: "sales-quotations",
    category: "sales",
    moduleId: "sales",
    path: "/sales/quotations",
    labelKey: "tabs.quotations",
    fallbackLabel: "عروض الأسعار",
    icon: "description",
    permission: { anyOf: ["sales.quotation.read", "sales.quotation.create", "sales.quotation.edit", "sales.quotation.open", "sales.quotation.accept", "sales.quotation.decline", "sales.quotation.revise", "sales.quotation.cancel", "sales.quotation.convert_invoice", "sales.quotation.convert_order"] },
    order: 20,
    mode: "mixed",
    keywords: ["عرض سعر", "عروض", "تسعير", "quotations", "quotes"]
  },
  {
    id: "sales-orders",
    category: "sales",
    moduleId: "sales",
    path: "/sales/orders",
    labelKey: "tabs.orders",
    fallbackLabel: "أوامر البيع والتنفيذ",
    icon: "shopping_cart",
    permission: { anyOf: ["sales.order.read", "sales.order.create", "sales.order.edit", "sales.order.confirm", "sales.order.amend", "sales.order.cancel", "sales.order.fulfill", "sales.order.invoice"] },
    order: 30,
    mode: "mixed",
    keywords: ["أمر بيع", "أوامر", "تنفيذ", "شحن", "orders", "fulfillment"]
  },
  {
    id: "sales-returns",
    category: "sales",
    moduleId: "sales",
    path: "/sales/returns",
    labelKey: "tabs.returns",
    fallbackLabel: "مرتجعات المبيعات",
    icon: "assignment_return",
    permission: { anyOf: ["sales.return.create", "sales.return.approve", "sales.return.execute", "sales.history.read"] },
    order: 40,
    mode: "mixed",
    keywords: ["مرتجع", "استرجاع", "مبيعات", "returns"]
  },
  {
    id: "sales-history",
    category: "sales",
    moduleId: "sales",
    path: "/sales/history",
    labelKey: "tabs.history",
    fallbackLabel: "سجل العمليات والتدقيق",
    icon: "history",
    permission: { anyOf: ["sales.history.read", "sales.audit.read"] },
    order: 50,
    mode: "read",
    keywords: ["سجل", "تدقيق", "تاريخ المبيعات", "history", "audit"]
  },
  {
    id: "sales-settings",
    category: "sales",
    moduleId: "sales",
    path: "/sales/settings",
    labelKey: "tabs.settings",
    fallbackLabel: "إعدادات وملفات العملاء",
    icon: "settings",
    permission: { anyOf: ["sales.customer_profile.manage", "sales.channel.manage", "sales.team.manage"] },
    order: 60,
    mode: "mutation",
    keywords: ["إعدادات", "ملف العميل", "فرق البيع", "قنوات البيع", "settings", "channels"]
  }
];

export const PURCHASING_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "purchasing-overview",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing",
    labelKey: "tabs.overview",
    fallbackLabel: "نظرة عامة على المشتريات",
    icon: "purchasing",
    permission: { anyOf: ["purchasing.po.read", "purchasing.receipt.view", "purchasing.receipt.request", "purchasing.match.read", "purchasing.history.read"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["مشتريات", "توريد", "نظرة عامة", "purchasing", "overview", "spend"]
  },
  {
    id: "purchasing-orders",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/orders",
    labelKey: "tabs.orders",
    fallbackLabel: "أوامر الشراء والتوريد",
    icon: "shopping_bag",
    permission: { anyOf: ["purchasing.po.read", "purchasing.po.create", "purchasing.po.edit", "purchasing.po.approve", "purchasing.po.reject", "purchasing.po.amend", "purchasing.po.cancel", "purchasing.po.close"] },
    order: 20,
    mode: "mixed",
    keywords: ["أمر شراء", "أوامر", "طلب توريد", "purchase orders", "po"]
  },
  {
    id: "purchasing-receipts",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/receipts",
    labelKey: "tabs.receipts",
    fallbackLabel: "استلام البضائع والتوريدات",
    icon: "local_shipping",
    permission: { anyOf: ["purchasing.receipt.request", "purchasing.receipt.view"] },
    order: 30,
    mode: "mixed",
    keywords: ["استلام", "إذن استلام", "فحص توريد", "goods receipt", "grn"]
  },
  {
    id: "purchasing-matching",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/matching",
    labelKey: "tabs.matching",
    fallbackLabel: "مطابقة الفواتير وأوامر الشراء",
    icon: "rule",
    permission: { anyOf: ["purchasing.match.read", "purchasing.match.override"] },
    order: 40,
    mode: "mixed",
    keywords: ["مطابقة", "مطابقة ثلاثية", "فروق أسعار", "invoice matching", "3-way match"]
  },
  {
    id: "purchasing-returns",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/returns",
    labelKey: "tabs.returns",
    fallbackLabel: "مرتجعات الموردين",
    icon: "assignment_return",
    permission: { anyOf: ["purchasing.return.create", "purchasing.return.approve", "purchasing.return.execute", "purchasing.po.read", "purchasing.history.read"] },
    order: 50,
    mode: "mixed",
    keywords: ["مرتجع مورد", "رد بضاعة", "supplier returns"]
  },
  {
    id: "purchasing-suppliers",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/suppliers",
    labelKey: "tabs.suppliers",
    fallbackLabel: "ملفات الموردين والشروط",
    icon: "people",
    permission: { anyOf: ["purchasing.supplier_profile.manage", "purchasing.po.read"] },
    order: 60,
    mode: "mixed",
    keywords: ["موردين", "شروط دفع", "ملف مورد", "suppliers", "vendor profiles"]
  },
  {
    id: "purchasing-history",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/history",
    labelKey: "tabs.history",
    fallbackLabel: "سجل الشراء والتدقيق",
    icon: "history",
    permission: { anyOf: ["purchasing.history.read", "purchasing.audit.read"] },
    order: 70,
    mode: "read",
    keywords: ["سجل شراء", "تاريخ", "تدقيق", "purchasing history", "audit"]
  },
  {
    id: "purchasing-settings",
    category: "purchasing",
    moduleId: "purchasing",
    path: "/purchasing/settings",
    labelKey: "tabs.settings",
    fallbackLabel: "إعدادات وسياسات الشراء",
    icon: "settings",
    permission: { anyOf: ["purchasing.settings.manage"] },
    order: 80,
    mode: "mutation",
    keywords: ["إعدادات شراء", "سياسات", "سماحيات", "purchasing settings"]
  }
];

export const POS_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "pos-checkout",
    category: "inventory",
    moduleId: "pos",
    path: "/pos",
    labelKey: "tabs.checkout",
    fallbackLabel: "نقطة البيع السريعة (الكاشير)",
    icon: "point_of_sale",
    permission: { anyOf: ["pos.checkout.create", "pos.cart.manage"] },
    order: 10,
    isPrimary: true,
    mode: "mixed",
    keywords: ["كاشير", "دفع سريع", "سلة", "فاتورة فورية", "pos", "checkout", "fastlane"]
  },
  {
    id: "pos-sessions",
    category: "inventory",
    moduleId: "pos",
    path: "/pos/sessions",
    labelKey: "tabs.sessions",
    fallbackLabel: "جلسات الكاشير وحركات النقدية",
    icon: "account_balance_wallet",
    permission: { anyOf: ["pos.session.open", "pos.session.close", "pos.cash.manage"] },
    order: 20,
    mode: "mixed",
    keywords: ["جلسة", "إغلاق وردية", "جرد نقدية", "عجز وزيادة", "sessions", "float", "drawer"]
  },
  {
    id: "pos-registers",
    category: "inventory",
    moduleId: "pos",
    path: "/pos/registers",
    labelKey: "tabs.registers",
    fallbackLabel: "إدارة نقاط البيع والأجهزة",
    icon: "desktop_windows",
    permission: { anyOf: ["pos.register.read", "pos.register.manage"] },
    order: 30,
    mode: "mixed",
    keywords: ["أجهزة", "طابعات", "نقاط بيع", "registers", "terminals"]
  },
  {
    id: "pos-returns",
    category: "inventory",
    moduleId: "pos",
    path: "/pos/returns",
    labelKey: "tabs.returns",
    fallbackLabel: "المرتجعات واستبدال المشتريات",
    icon: "assignment_return",
    permission: { anyOf: ["pos.return.create", "pos.exchange.create"] },
    order: 40,
    mode: "mixed",
    keywords: ["مرتجع", "استبدال", "رد أموال", "pos returns", "exchange"]
  },
  {
    id: "pos-offline",
    category: "inventory",
    moduleId: "pos",
    path: "/pos/offline",
    labelKey: "tabs.offline",
    fallbackLabel: "المزامنة دون اتصال وحل التعارض",
    icon: "sync_problem",
    permission: { anyOf: ["pos.offline.sync", "pos.override.approve"] },
    order: 50,
    mode: "mixed",
    keywords: ["دون اتصال", "مزامنة", "تعارض", "offline sync", "conflicts"]
  },
  {
    id: "pos-reports",
    category: "inventory",
    moduleId: "pos",
    path: "/pos/reports",
    labelKey: "tabs.reports",
    fallbackLabel: "تقارير المبيعات والنقدية (POS)",
    icon: "bar_chart",
    permission: { anyOf: ["pos.report.read"] },
    order: 60,
    mode: "read",
    keywords: ["تقارير كاشير", "مبيعات يومية", "ملخص الوردية", "z-report", "pos reports"]
  }
];

export const FIXED_ASSETS_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "fixed-assets-overview", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets",
    labelKey: "tabs.overview", fallbackLabel: "نظرة عامة على الأصول", icon: "fixed-assets",
    permission: { anyOf: ["assets.read", "assets.financial.read", "assets.audit.read"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["أصول", "أصول ثابتة", "fixed assets", "overview"]
  },
  {
    id: "fixed-assets-register", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/register",
    labelKey: "tabs.register", fallbackLabel: "سجل الأصول", icon: "fixed-assets",
    permission: { anyOf: ["assets.read", "assets.create", "assets.edit"] }, order: 20, mode: "mixed",
    keywords: ["سجل الأصول", "asset register", "assets"]
  },
  {
    id: "fixed-assets-custody", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/custody",
    labelKey: "tabs.custody", fallbackLabel: "العهد والتحقق الميداني", icon: "verified",
    permission: { anyOf: ["assets.read", "assets.assign", "assets.verification.manage"] }, order: 30, mode: "mixed",
    keywords: ["عهدة", "تحقق", "custody", "verification"]
  },
  {
    id: "fixed-assets-capitalization", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/capitalization",
    labelKey: "tabs.capitalization", fallbackLabel: "الرسملة والمشروعات الرأسمالية", icon: "layers",
    permission: { anyOf: ["assets.read", "assets.capitalize", "assets.opening.manage"] }, order: 40, mode: "mixed",
    keywords: ["رسملة", "مشروعات رأسمالية", "CIP", "capitalization"]
  },
  {
    id: "fixed-assets-depreciation", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/depreciation",
    labelKey: "tabs.depreciation", fallbackLabel: "الاستهلاك والدفاتر", icon: "calculate",
    permission: { anyOf: ["assets.financial.read", "assets.book.manage", "assets.depreciation.prepare", "assets.depreciation.post", "assets.depreciation.reverse"] }, order: 50, mode: "mixed",
    keywords: ["استهلاك", "دفاتر أصول", "depreciation", "books"]
  },
  {
    id: "fixed-assets-reports", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/reports",
    labelKey: "tabs.reports", fallbackLabel: "تقارير الأصول والمطابقة", icon: "reports",
    permission: { anyOf: ["assets.financial.read", "assets.audit.read", "assets.export"] }, order: 60, mode: "read",
    keywords: ["تقارير", "مطابقة", "asset reports", "reconciliation"]
  },
  {
    id: "fixed-assets-my-assets", category: "finance", moduleId: "fixed-assets", path: "/fixed-assets/my-assets",
    labelKey: "tabs.myAssets", fallbackLabel: "أصولي وعهد العمل", icon: "person",
    permission: "assets.read", order: 70, mode: "read",
    keywords: ["أصولي", "عهد العمل", "my assets"]
  }
];

export const EXPENSES_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "expenses-overview", category: "finance", moduleId: "expenses", path: "/expenses",
    labelKey: "tabs.overview", fallbackLabel: "نظرة عامة على المصروفات", icon: "expenses",
    permission: { anyOf: ["expenses.read", "expenses.create", "expenses.reimbursement.read", "expenses.reimbursement.manage"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["مصروفات", "نفقات", "expenses", "overview"]
  },
  {
    id: "expenses-self-service", category: "finance", moduleId: "expenses", path: "/expenses/self-service",
    labelKey: "tabs.selfService", fallbackLabel: "مصروفاتي وطلبات السداد", icon: "person",
    permission: { anyOf: ["expenses.read", "expenses.create", "expenses.submit", "expenses.reimbursement.read"] }, order: 20, mode: "mixed",
    keywords: ["مصروفاتي", "طلب سداد", "self service", "reimbursement"]
  },
  {
    id: "expenses-review", category: "finance", moduleId: "expenses", path: "/expenses/review",
    labelKey: "tabs.review", fallbackLabel: "مراجعة واعتماد المصروفات", icon: "check",
    permission: { anyOf: ["expenses.approve", "expenses.finalize", "expenses.audit.read"] }, order: 30, mode: "mixed",
    keywords: ["مراجعة", "اعتماد", "مصروفات", "review", "approval"]
  },
  {
    id: "expenses-advances", category: "finance", moduleId: "expenses", path: "/expenses/advances",
    labelKey: "tabs.advances", fallbackLabel: "السلف وتسويتها", icon: "banknotes",
    permission: { anyOf: ["expenses.reimbursement.manage", "expenses.approve", "expenses.finalize"] }, order: 40, mode: "mixed",
    keywords: ["سلف", "تسوية سلفة", "advances"]
  },
  {
    id: "expenses-corporate-cards", category: "finance", moduleId: "expenses", path: "/expenses/corporate-cards",
    labelKey: "tabs.corporateCards", fallbackLabel: "البطاقات المؤسسية", icon: "credit-card",
    permission: { anyOf: ["expenses.read", "expenses.approve", "expenses.finalize"] }, order: 50, mode: "mixed",
    keywords: ["بطاقات", "بطاقة مؤسسية", "corporate cards"]
  },
  {
    id: "expenses-reimbursements", category: "finance", moduleId: "expenses", path: "/expenses/reimbursements",
    labelKey: "tabs.reimbursements", fallbackLabel: "السداد والتعويضات", icon: "transfer",
    permission: { anyOf: ["expenses.reimbursement.read", "expenses.reimbursement.manage", "expenses.finalize"] }, order: 60, mode: "mixed",
    keywords: ["سداد", "تعويض", "reimbursements"]
  },
  {
    id: "expenses-reports", category: "finance", moduleId: "expenses", path: "/expenses/reports",
    labelKey: "tabs.reports", fallbackLabel: "تقارير المصروفات", icon: "reports",
    permission: { anyOf: ["expenses.audit.read", "expenses.read"] }, order: 70, mode: "read",
    keywords: ["تقارير مصروفات", "expenses reports"]
  },
  {
    id: "expenses-settings", category: "finance", moduleId: "expenses", path: "/expenses/settings",
    labelKey: "tabs.settings", fallbackLabel: "إعدادات المصروفات", icon: "settings",
    permission: "expenses.category.manage", order: 80, mode: "mutation",
    keywords: ["إعدادات المصروفات", "سياسات المصروفات", "expense settings"]
  }
];

export const INVOICING_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "invoicing-overview", category: "sales", moduleId: "invoicing", path: "/invoicing",
    labelKey: "tabs.overview", fallbackLabel: "نظرة عامة على الفوترة", icon: "invoicing",
    permission: { anyOf: ["invoicing.customer_invoice.read", "invoicing.supplier_invoice.read", "invoicing.customer_invoice.create", "invoicing.supplier_invoice.create"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["فواتير", "فوترة", "invoicing", "billing"]
  },
  {
    id: "invoicing-customer", category: "sales", moduleId: "invoicing", path: "/invoicing/customer-invoices",
    labelKey: "tabs.customerInvoices", fallbackLabel: "فواتير العملاء", icon: "receipt",
    permission: { anyOf: ["invoicing.customer_invoice.read", "invoicing.customer_invoice.create", "invoicing.customer_invoice.edit", "invoicing.customer_invoice.finalize"] }, order: 20, mode: "mixed",
    keywords: ["فاتورة عميل", "فواتير مبيعات", "customer invoices"]
  },
  {
    id: "invoicing-supplier", category: "sales", moduleId: "invoicing", path: "/invoicing/supplier-invoices",
    labelKey: "tabs.supplierInvoices", fallbackLabel: "فواتير الموردين", icon: "receipt",
    permission: { anyOf: ["invoicing.supplier_invoice.read", "invoicing.supplier_invoice.create", "invoicing.supplier_invoice.edit", "invoicing.supplier_invoice.finalize"] }, order: 30, mode: "mixed",
    keywords: ["فاتورة مورد", "فواتير مشتريات", "supplier invoices"]
  }
];

export const PAYMENTS_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "payments-overview", category: "finance", moduleId: "payments", path: "/payments",
    labelKey: "tabs.overview", fallbackLabel: "نظرة عامة على المدفوعات", icon: "payments",
    permission: { anyOf: ["payments.read", "payments.incoming.create", "payments.outgoing.create", "payments.account.read"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["مدفوعات", "تحصيل", "صرف", "payments"]
  },
  {
    id: "payments-incoming", category: "finance", moduleId: "payments", path: "/payments/incoming",
    labelKey: "tabs.incoming", fallbackLabel: "سندات القبض والتحصيل", icon: "arrow-down",
    permission: { anyOf: ["payments.read", "payments.incoming.create", "payments.complete_manual"] }, order: 20, mode: "mixed",
    keywords: ["سند قبض", "تحصيل", "incoming payments"]
  },
  {
    id: "payments-outgoing", category: "finance", moduleId: "payments", path: "/payments/outgoing",
    labelKey: "tabs.outgoing", fallbackLabel: "سندات الصرف للموردين", icon: "arrow-up",
    permission: { anyOf: ["payments.read", "payments.outgoing.create", "payments.complete_manual"] }, order: 30, mode: "mixed",
    keywords: ["سند صرف", "صرف مورد", "outgoing payments"]
  },
  {
    id: "payments-transfers", category: "finance", moduleId: "payments", path: "/payments/transfers",
    labelKey: "tabs.transfers", fallbackLabel: "التحويلات الداخلية", icon: "transfer",
    permission: { anyOf: ["payments.read", "payments.internal_transfer.create"] }, order: 40, mode: "mixed",
    keywords: ["تحويل داخلي", "internal transfer"]
  },
  {
    id: "payments-accounts", category: "finance", moduleId: "payments", path: "/payments/accounts",
    labelKey: "tabs.accounts", fallbackLabel: "حسابات الدفع", icon: "account-balance",
    permission: { anyOf: ["payments.account.read", "payments.account.manage"] }, order: 50, mode: "mixed",
    keywords: ["حسابات الدفع", "payment accounts"]
  },
  {
    id: "payments-requests", category: "finance", moduleId: "payments", path: "/payments/requests",
    labelKey: "tabs.requests", fallbackLabel: "طلبات الدفع الإلكتروني", icon: "link",
    permission: { anyOf: ["payments.provider.read", "payments.provider.manage", "payments.provider.retry", "payments.provider.reconcile", "payments.incoming.create"] }, order: 60, mode: "mixed",
    keywords: ["طلبات دفع", "بوابة دفع", "payment requests"]
  }
];

export const HR_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "hr-overview", category: "hr", moduleId: "hr", path: "/hr",
    labelKey: "tabs.home", fallbackLabel: "نظرة عامة على الموارد البشرية", icon: "users",
    permission: { anyOf: ["hr.home.read", "hr.employee.read_basic", "hr.employee.read_self", "hr.employee.read_team"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["موارد بشرية", "موظفون", "HR", "overview"]
  },
  {
    id: "hr-directory", category: "hr", moduleId: "hr", path: "/hr/directory",
    labelKey: "tabs.directory", fallbackLabel: "دليل الموظفين", icon: "users",
    permission: { anyOf: ["hr.employee.read_basic", "hr.employee.create", "hr.employee.manage"] }, order: 20, mode: "mixed",
    keywords: ["موظفين", "دليل الموظفين", "employee directory"]
  },
  {
    id: "hr-attendance", category: "hr", moduleId: "hr", path: "/hr/attendance",
    labelKey: "tabs.attendance", fallbackLabel: "الحضور والانصراف", icon: "schedule",
    permission: { anyOf: ["hr.attendance.read", "hr.attendance.read_self", "hr.attendance.manage", "hr.attendance.correct"] }, order: 30, mode: "mixed",
    keywords: ["حضور", "انصراف", "attendance"]
  },
  {
    id: "hr-leave", category: "hr", moduleId: "hr", path: "/hr/leave",
    labelKey: "tabs.leave", fallbackLabel: "طلبات الإجازات", icon: "event",
    permission: { anyOf: ["hr.leave.read", "hr.leave.read_self", "hr.leave.read_team", "hr.leave.request_self", "hr.leave.approve_team", "hr.leave.approve"] }, order: 40, mode: "mixed",
    keywords: ["إجازات", "طلب إجازة", "leave"]
  },
  {
    id: "hr-organization", category: "hr", moduleId: "hr", path: "/hr/organization",
    labelKey: "tabs.organization", fallbackLabel: "الهيكل التنظيمي", icon: "account-tree",
    permission: { anyOf: ["hr.department.read", "hr.department.manage", "hr.position.read", "hr.position.manage", "hr.assignment.read", "hr.assignment.manage"] }, order: 50, mode: "mixed",
    keywords: ["هيكل تنظيمي", "إدارات", "وظائف", "organization"]
  },
  {
    id: "hr-self-service", category: "hr", moduleId: "hr", path: "/hr/self-service",
    labelKey: "tabs.selfservice", fallbackLabel: "الخدمة الذاتية للموظف", icon: "person",
    permission: { anyOf: ["hr.employee.read_self", "hr.leave.read_self", "hr.leave.request_self", "hr.attendance.read_self", "hr.employee.document.read_self"] }, order: 60, mode: "mixed",
    keywords: ["خدمة ذاتية", "موظف", "self service"]
  },
  {
    id: "hr-manager", category: "hr", moduleId: "hr", path: "/hr/manager",
    labelKey: "tabs.manager", fallbackLabel: "إدارة الفريق", icon: "supervisor",
    permission: { anyOf: ["hr.employee.read_team", "hr.leave.read_team", "hr.leave.approve_team", "hr.attendance.manage"] }, order: 70, mode: "mixed",
    keywords: ["إدارة الفريق", "اعتماد الإجازات", "manager"]
  },
  {
    id: "hr-reports", category: "hr", moduleId: "hr", path: "/hr/reports",
    labelKey: "tabs.reports", fallbackLabel: "تقارير الموارد البشرية", icon: "reports",
    permission: { anyOf: ["hr.report.read", "hr.audit.read", "hr.export"] }, order: 80, mode: "read",
    keywords: ["تقارير الموارد البشرية", "HR reports"]
  },
  {
    id: "hr-imports", category: "hr", moduleId: "hr", path: "/hr/imports",
    labelKey: "tabs.imports", fallbackLabel: "استيراد بيانات الموظفين", icon: "upload",
    permission: "hr.employee.import", order: 90, mode: "mutation",
    keywords: ["استيراد موظفين", "employee import"]
  }
];

export const PAYROLL_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "payroll-overview", category: "hr", moduleId: "payroll", path: "/payroll",
    labelKey: "tabs.home", fallbackLabel: "نظرة عامة على الرواتب", icon: "banknotes",
    permission: { anyOf: ["payroll.home.read", "payroll.run.read", "payroll.payslip.read_self", "payroll.payslip.read"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["رواتب", "أجور", "payroll", "overview"]
  },
  {
    id: "payroll-runs", category: "hr", moduleId: "payroll", path: "/payroll/runs",
    labelKey: "tabs.runs", fallbackLabel: "مسيرات الرواتب", icon: "play",
    permission: { anyOf: ["payroll.run.read", "payroll.run.create", "payroll.run.calculate", "payroll.run.review", "payroll.run.approve", "payroll.run.finalize"] }, order: 20, mode: "mixed",
    keywords: ["مسير رواتب", "دورة رواتب", "payroll runs"]
  },
  {
    id: "payroll-inputs", category: "hr", moduleId: "payroll", path: "/payroll/inputs",
    labelKey: "tabs.inputs", fallbackLabel: "مدخلات الرواتب", icon: "edit",
    permission: { anyOf: ["payroll.input.read", "payroll.input.create", "payroll.input.approve"] }, order: 30, mode: "mixed",
    keywords: ["مدخلات", "بدلات", "استقطاعات", "payroll inputs"]
  },
  {
    id: "payroll-loans", category: "hr", moduleId: "payroll", path: "/payroll/loans",
    labelKey: "tabs.loans", fallbackLabel: "سلف وقروض الموظفين", icon: "account-balance",
    permission: { anyOf: ["payroll.loan.read", "payroll.loan.create", "payroll.loan.approve", "payroll.loan.reschedule", "payroll.loan.writeoff"] }, order: 40, mode: "mixed",
    keywords: ["سلف", "قروض", "employee loans"]
  },
  {
    id: "payroll-payslips", category: "hr", moduleId: "payroll", path: "/payroll/payslips",
    labelKey: "tabs.payslips", fallbackLabel: "قسائم الرواتب", icon: "receipt",
    permission: { anyOf: ["payroll.payslip.read", "payroll.payslip.read_self", "payroll.payslip.export"] }, order: 50, mode: "read",
    keywords: ["قسيمة راتب", "كشف راتب", "payslips"]
  },
  {
    id: "payroll-reports", category: "hr", moduleId: "payroll", path: "/payroll/reports",
    labelKey: "tabs.reports", fallbackLabel: "تقارير الرواتب والامتثال", icon: "reports",
    permission: { anyOf: ["payroll.compliance_output.read", "payroll.accounting_preview.read", "payroll.audit.read", "payroll.compliance_output.generate"] }, order: 60, mode: "read",
    keywords: ["تقارير رواتب", "امتثال", "payroll reports"]
  },
  {
    id: "payroll-setup", category: "hr", moduleId: "payroll", path: "/payroll/setup",
    labelKey: "tabs.setup", fallbackLabel: "إعدادات مسير الرواتب", icon: "settings",
    permission: { anyOf: ["payroll.group.manage", "payroll.structure.manage", "payroll.component.manage", "payroll.assignment.manage"] }, order: 70, mode: "mutation",
    keywords: ["إعداد الرواتب", "هيكل راتب", "payroll setup"]
  },
  {
    id: "payroll-settings", category: "hr", moduleId: "payroll", path: "/payroll/settings",
    labelKey: "tabs.settings", fallbackLabel: "إعدادات الرواتب النظامية", icon: "settings",
    permission: { anyOf: ["payroll.settings.read", "payroll.settings.manage"] }, order: 80, mode: "mixed",
    keywords: ["إعدادات الرواتب", "ضوابط نظامية", "payroll settings"]
  },
  {
    id: "payroll-opening-balances", category: "hr", moduleId: "payroll", path: "/payroll/opening-balances",
    labelKey: "tabs.openingBalances", fallbackLabel: "الأرصدة الافتتاحية للرواتب", icon: "account-balance",
    permission: { anyOf: ["payroll.input.read", "payroll.input.create", "payroll.run.read"] }, order: 90, mode: "mixed",
    keywords: ["أرصدة افتتاحية", "opening balances"]
  }
];

export const PROJECTS_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "projects-overview", category: "inventory", moduleId: "projects", path: "/projects",
    labelKey: "tabs.portfolio", fallbackLabel: "محفظة المشاريع", icon: "projects",
    permission: { anyOf: ["projects.read", "projects.create", "projects.budget.read", "projects.cost.read"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["مشاريع", "محفظة", "projects", "portfolio"]
  },
  {
    id: "projects-resource-load", category: "inventory", moduleId: "projects", path: "/projects/resource-load",
    labelKey: "tabs.resourceLoad", fallbackLabel: "تحميل الموارد", icon: "groups",
    permission: { anyOf: ["projects.read", "projects.manage_team"] }, order: 20, mode: "read",
    keywords: ["موارد", "تحميل الموارد", "resource load"]
  },
  {
    id: "projects-templates", category: "inventory", moduleId: "projects", path: "/projects/templates",
    labelKey: "tabs.templates", fallbackLabel: "قوالب المشاريع", icon: "description",
    permission: { anyOf: ["projects.read", "projects.create", "projects.edit"] }, order: 30, mode: "mixed",
    keywords: ["قوالب مشاريع", "project templates"]
  }
];

export const SERVICE_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "service-overview", category: "inventory", moduleId: "service", path: "/service",
    labelKey: "tabs.overview", fallbackLabel: "نظرة عامة على الخدمة", icon: "service",
    permission: { anyOf: ["service.request.read", "service.work.read", "service.dispatch.read", "service.installed_base.read"] }, order: 10, isPrimary: true, mode: "read",
    keywords: ["خدمة", "صيانة", "service", "overview"]
  },
  {
    id: "service-requests", category: "inventory", moduleId: "service", path: "/service/requests",
    labelKey: "tabs.requests", fallbackLabel: "طلبات الخدمة", icon: "inbox",
    permission: { anyOf: ["service.request.read", "service.request.create", "service.request.triage", "service.request.resolve"] }, order: 20, mode: "mixed",
    keywords: ["طلب خدمة", "بلاغ", "service requests"]
  },
  {
    id: "service-work-orders", category: "inventory", moduleId: "service", path: "/service/work-orders",
    labelKey: "tabs.workOrders", fallbackLabel: "أوامر العمل", icon: "assignment",
    permission: { anyOf: ["service.work.read", "service.work.create", "service.work.plan", "service.work.schedule", "service.work.execute", "service.work.complete", "service.work.close"] }, order: 30, mode: "mixed",
    keywords: ["أمر عمل", "work order"]
  },
  {
    id: "service-dispatch", category: "inventory", moduleId: "service", path: "/service/dispatch",
    labelKey: "tabs.dispatch", fallbackLabel: "جدولة وتوزيع الفنيين", icon: "route",
    permission: { anyOf: ["service.dispatch.read", "service.dispatch.manage"] }, order: 40, mode: "mixed",
    keywords: ["جدولة", "فنيين", "dispatch"]
  },
  {
    id: "service-my-day", category: "inventory", moduleId: "service", path: "/service/my-day",
    labelKey: "tabs.myDay", fallbackLabel: "يومي الميداني", icon: "today",
    permission: { anyOf: ["service.work.read", "service.work.execute", "service.work.complete"] }, order: 50, mode: "mixed",
    keywords: ["يومي", "تنفيذ ميداني", "technician day"]
  },
  {
    id: "service-installed-base", category: "inventory", moduleId: "service", path: "/service/installed-base",
    labelKey: "tabs.installedBase", fallbackLabel: "الأصول المركبة", icon: "inventory",
    permission: { anyOf: ["service.installed_base.read", "service.installed_base.manage"] }, order: 60, mode: "mixed",
    keywords: ["أصول مركبة", "installed base"]
  },
  {
    id: "service-preventive", category: "inventory", moduleId: "service", path: "/service/preventive",
    labelKey: "tabs.preventive", fallbackLabel: "الصيانة الوقائية", icon: "schedule",
    permission: { anyOf: ["service.maintenance_plan.read", "service.maintenance_plan.manage", "service.meter.read", "service.meter.record"] }, order: 70, mode: "mixed",
    keywords: ["صيانة وقائية", "خطة صيانة", "preventive maintenance"]
  },
  {
    id: "service-contracts", category: "inventory", moduleId: "service", path: "/service/contracts-warranty",
    labelKey: "tabs.contractsWarranty", fallbackLabel: "العقود والضمان", icon: "verified",
    permission: { anyOf: ["service.contract.read", "service.contract.manage", "service.warranty.read", "service.warranty.manage"] }, order: 80, mode: "mixed",
    keywords: ["عقد خدمة", "ضمان", "service contracts", "warranty"]
  },
  {
    id: "service-billing", category: "inventory", moduleId: "service", path: "/service/billing",
    labelKey: "tabs.billing", fallbackLabel: "فوترة الخدمة", icon: "receipt",
    permission: { anyOf: ["service.billing.read", "service.billing.manage", "service.billing.request_invoice", "service.billing.writeoff"] }, order: 90, mode: "mixed",
    keywords: ["فوترة خدمة", "service billing"]
  }
];

export const QUALITY_DESTINATIONS: readonly NavDestination[] = [
  {
    id: "quality-overview",
    category: "inventory",
    moduleId: "quality",
    path: "/quality",
    labelKey: "tabs.overview",
    fallbackLabel: "لوحة التحكم وأولويات العمل",
    icon: "dashboard",
    permission: { anyOf: ["quality.read", "quality.inspection.create", "quality.inspection.execute", "quality.inspection.review", "quality.hold.manage", "quality.disposition.manage", "quality.ncr.manage", "quality.capa.manage"] },
    order: 10,
    isPrimary: true,
    mode: "read",
    keywords: ["جودة", "لوحة التحكم", "أولويات", "نظرة عامة", "quality", "overview"]
  },
  {
    id: "quality-inspections",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/inspections",
    labelKey: "tabs.inspections",
    fallbackLabel: "فحوصات الجودة",
    icon: "checklist",
    permission: { anyOf: ["quality.inspection.create", "quality.inspection.execute", "quality.inspection.review", "quality.read"] },
    order: 20,
    mode: "mixed",
    keywords: ["فحص", "فحوصات", "عينة", "قياس", "inspections", "samples"]
  },
  {
    id: "quality-plans",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/plans",
    labelKey: "tabs.plans",
    fallbackLabel: "خطط ومعايير الجودة",
    icon: "assignment",
    permission: { anyOf: ["quality.plan.manage", "quality.characteristic.manage", "quality.read"] },
    order: 30,
    mode: "mixed",
    keywords: ["خطة جودة", "مواصفة", "خاصية فحص", "عينة", "plans", "specifications"]
  },
  {
    id: "quality-restrictions",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/restrictions",
    labelKey: "tabs.restrictions",
    fallbackLabel: "الحظر واحتجاز الجودة (Holds)",
    icon: "lock",
    permission: { anyOf: ["quality.hold.manage", "quality.read"] },
    order: 40,
    mode: "mixed",
    keywords: ["حظر", "حجز مخزني", "إفراج", "holds", "restrictions", "quarantine"]
  },
  {
    id: "quality-dispositions",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/dispositions",
    labelKey: "tabs.dispositions",
    fallbackLabel: "القرارات الفنية (Dispositions)",
    icon: "gavel",
    permission: { anyOf: ["quality.disposition.manage", "quality.read"] },
    order: 50,
    mode: "mixed",
    keywords: ["تصرف", "قرار فني", "إرجاع", "خردة", "disposition", "approval"]
  },
  {
    id: "quality-ncrs",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/ncrs",
    labelKey: "tabs.ncrs",
    fallbackLabel: "حالات عدم المطابقة (NCR)",
    icon: "warning",
    permission: { anyOf: ["quality.ncr.manage", "quality.issue.manage", "quality.read"] },
    order: 60,
    mode: "mixed",
    keywords: ["عدم مطابقة", "مشاكل الجودة", "احتواء", "ncr", "issues", "nonconformance"]
  },
  {
    id: "quality-capas",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/capas",
    labelKey: "tabs.capas",
    fallbackLabel: "الإجراءات التصحيحية والوقائية (CAPA)",
    icon: "published_with_changes",
    permission: { anyOf: ["quality.capa.manage", "quality.read"] },
    order: 70,
    mode: "mixed",
    keywords: ["إجراء تصحيحي", "إجراء وقائي", "جذر المشكلة", "فعالية", "capa", "root cause"]
  },
  {
    id: "quality-supplier-quality",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/supplier-quality",
    labelKey: "tabs.supplierQuality",
    fallbackLabel: "جودة الموردين و(SCAR)",
    icon: "local_shipping",
    permission: { anyOf: ["quality.supplier.manage", "quality.read"] },
    order: 80,
    mode: "mixed",
    keywords: ["مورد", "تقييم مورد", "scar", "supplier quality"]
  },
  {
    id: "quality-certificates",
    category: "inventory",
    moduleId: "quality",
    path: "/quality/certificates",
    labelKey: "tabs.certificates",
    fallbackLabel: "شهادات التحليل والمطابقة (COA)",
    icon: "verified",
    permission: { anyOf: ["quality.certificate.manage", "quality.read"] },
    order: 90,
    mode: "mixed",
    keywords: ["شهادة", "coa", "مطابقة", "تحليل", "certificates"]
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
