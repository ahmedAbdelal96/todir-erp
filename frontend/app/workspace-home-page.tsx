import { Link, useOutletContext } from "react-router-dom";
import { PageHeader } from "../components/pages/page-header.tsx";
import { AccountingIcon, DirectoryIcon, InvoicingIcon, MigrationIcon, UsersIcon } from "../components/primitives/icons.tsx";
import type { WorkspaceOutletContext } from "./router/route-contract.ts";

const cards = [
  { path: "/users", icon: UsersIcon, ar: { title: "المستخدمون", description: "إدارة حسابات الفريق والصلاحيات" }, en: { title: "Users", description: "Manage team accounts and permissions" } },
  { path: "/directory", icon: DirectoryIcon, ar: { title: "دليل الأطراف", description: "العملاء والموردون وجهات الاتصال" }, en: { title: "Directory", description: "Customers, suppliers, and contacts" } },
  { path: "/invoicing", icon: InvoicingIcon, ar: { title: "فواتير المبيعات", description: "الفواتير الضريبية والمطالبات" }, en: { title: "Sales invoices", description: "Tax invoices and receivables" } },
  { path: "/accounting", icon: AccountingIcon, ar: { title: "قيود اليومية", description: "المحاسبة العامة ودفتر الأستاذ" }, en: { title: "Journal entries", description: "General ledger and accounting" } },
  { path: "/migration", icon: MigrationIcon, ar: { title: "معالج الاستيراد", description: "استيراد البيانات وتجهيز النظام" }, en: { title: "Import wizard", description: "Import data and prepare the system" } }
] as const;

export function WorkspaceHomePage(): React.JSX.Element {
  const { language } = useOutletContext<WorkspaceOutletContext>();
  const content = language === "ar";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <PageHeader
        title={content ? "مساحة العمل" : "Workspace"}
        subtitle={content ? "نظام تخطيط وإدارة موارد المؤسسة المالي والتشغيلي الموحد." : "Unified financial and operational enterprise resource planning system."}
      />
      <div>
        <h2 style={{ fontSize: "1.0625rem", fontWeight: 700, margin: "0 0 1rem 0", color: "var(--color-content-primary)" }}>
          {content ? "الوصول السريع للوحدات" : "Quick module launchpad"}
        </h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" }}>
          {cards.map(({ path, icon: Icon, ar, en }) => {
            const card = content ? ar : en;
            return (
              <Link key={path} to={path} style={launchpadCardStyle}>
                <span style={cardIconContainerStyle}><Icon size={22} /></span>
                <span style={{ flex: 1, textAlign: "start" }}>
                  <strong style={{ display: "block", fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>{card.title}</strong>
                  <span style={{ display: "block", marginTop: "0.25rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{card.description}</span>
                </span>
              </Link>
            );
          })}
        </div>
      </div>
    </div>
  );
}

const launchpadCardStyle: React.CSSProperties = {
  backgroundColor: "var(--color-surface-card)",
  border: "1px solid var(--color-border-default)",
  borderRadius: "var(--radius-lg)",
  padding: "1.25rem",
  display: "flex",
  alignItems: "center",
  gap: "1rem",
  cursor: "pointer",
  outline: "none",
  transition: "all 200ms ease",
  boxShadow: "var(--shadow-xs)",
  textDecoration: "none"
};

const cardIconContainerStyle: React.CSSProperties = {
  padding: "0.75rem",
  borderRadius: "var(--radius-md)",
  backgroundColor: "var(--color-brand-primary-subtle)",
  color: "var(--color-brand-primary)",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  flexShrink: 0
};
