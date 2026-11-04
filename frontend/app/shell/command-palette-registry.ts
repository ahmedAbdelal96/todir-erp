import type { NavigateFunction } from "react-router-dom";
import type { TFunction } from "i18next";
import type { CommandItem } from "../../components/primitives/command-palette.tsx";
import type { ModuleNavigationRegistration } from "../router/route-contract.ts";

export interface BuildCommandPaletteOptions {
  readonly modules: readonly ModuleNavigationRegistration[];
  readonly navigate: NavigateFunction;
  readonly t: TFunction;
  readonly onOpenQuickCreateParty?: () => void;
  readonly onOpenQuickCreateProduct?: () => void;
}

export function buildCommandPaletteItems({
  modules,
  navigate,
  t,
  onOpenQuickCreateParty,
  onOpenQuickCreateProduct
}: BuildCommandPaletteOptions): CommandItem[] {
  const items: CommandItem[] = [];

  // 1. Navigation items from registered and permitted modules
  for (const mod of modules) {
    const navKey = mod.navigationKey;
    const title = t(`navigation:${navKey}`, mod.id);
    items.push({
      id: `nav-${mod.id}`,
      title,
      category: "navigation",
      keywords: [mod.id, navKey, mod.path],
      onSelect: () => navigate(mod.path)
    });
  }

  // 2. High-frequency Quick Actions for enterprise daily workflows
  items.push({
    id: "action-new-customer-invoice",
    title: t("commandPalette.actionNewCustomerInvoice", "إنشاء فاتورة مبيعات جديدة"),
    subtitle: t("commandPalette.subtitleCustomerInvoices", "إدارة وإنشاء فواتير العملاء"),
    category: "actions",
    keywords: ["مبيعات", "فاتورة", "عميل", "بيع", "invoice", "sales", "customer"],
    shortcut: "Alt + I",
    onSelect: () => navigate("/invoicing/customer-invoices/new")
  });

  items.push({
    id: "action-new-supplier-invoice",
    title: t("commandPalette.actionNewSupplierInvoice", "إنشاء فاتورة مشتريات جديدة"),
    subtitle: t("commandPalette.subtitleSupplierInvoices", "إدارة وتسجيل فواتير الموردين"),
    category: "actions",
    keywords: ["مشتريات", "فاتورة", "مورد", "شراء", "bill", "purchase", "supplier"],
    shortcut: "Alt + B",
    onSelect: () => navigate("/invoicing/supplier-invoices/new")
  });

  items.push({
    id: "action-new-product",
    title: t("commandPalette.actionNewProduct", "إضافة منتج جديد للكتالوج"),
    subtitle: t("commandPalette.subtitleCatalog", "إدارة المنتجات والخدمات والأسعار"),
    category: "actions",
    keywords: ["منتج", "صنف", "كتالوج", "بضاعة", "product", "item", "catalog"],
    onSelect: () => {
      if (onOpenQuickCreateProduct) {
        onOpenQuickCreateProduct();
      } else {
        navigate("/catalog");
      }
    }
  });

  items.push({
    id: "action-new-party",
    title: t("commandPalette.actionNewParty", "إضافة عميل أو مورد جديد (دليل الجهات)"),
    subtitle: t("commandPalette.subtitleDirectory", "دليل العملاء والموردين والجهات"),
    category: "actions",
    keywords: ["عميل", "مورد", "جهة", "دليل", "customer", "supplier", "party", "directory"],
    onSelect: () => {
      if (onOpenQuickCreateParty) {
        onOpenQuickCreateParty();
      } else {
        navigate("/directory");
      }
    }
  });

  items.push({
    id: "action-open-subledger",
    title: t("commandPalette.actionOpenSubledger", "تسوية البنود والمطالبات المفتوحة"),
    subtitle: t("commandPalette.subtitleSubledger", "تسوية ومطابقة الحسابات المفتوحة"),
    category: "actions",
    keywords: ["تسوية", "مطالبات", "سداد", "مستحقات", "settlement", "subledger", "open items"],
    onSelect: () => navigate("/subledger")
  });

  items.push({
    id: "action-chart-of-accounts",
    title: t("commandPalette.actionChartOfAccounts", "دليل الحسابات وشجرة القيود"),
    subtitle: t("commandPalette.subtitleFinbase", "الدليل المحاسبي وشجرة الحسابات"),
    category: "navigation",
    keywords: ["شجرة", "حسابات", "دليل", "قيود", "chart of accounts", "finbase"],
    onSelect: () => navigate("/finbase")
  });

  items.push({
    id: "action-tax-declarations",
    title: t("commandPalette.actionTaxDeclarations", "الإقرارات الضريبية والقيمة المضافة"),
    subtitle: t("commandPalette.subtitleTax", "الإقرارات والضرائب والامتثال"),
    category: "navigation",
    keywords: ["ضريبة", "قيمة مضافة", "إقرار", "tax", "vat"],
    onSelect: () => navigate("/tax")
  });

  items.push({
    id: "action-payroll-run",
    title: t("commandPalette.actionPayrollRun", "إدارة دورات الرواتب والأجور"),
    subtitle: t("commandPalette.subtitlePayroll", "احتساب الرواتب، السلف وقسائم الموظفين"),
    category: "actions",
    keywords: ["رواتب", "أجور", "سلف", "مرتبات", "payroll", "salary", "payslip"],
    onSelect: () => navigate("/payroll")
  });

  return items;
}
