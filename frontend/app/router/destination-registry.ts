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
