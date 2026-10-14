import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { PageHeader } from "../components/pages/page-header.tsx";
import { AccountingIcon, DirectoryIcon, InvoicingIcon, MigrationIcon, UsersIcon } from "../components/primitives/icons.tsx";

const cards = [
  { path: "/users", icon: UsersIcon, key: "users" },
  { path: "/directory", icon: DirectoryIcon, key: "directory" },
  { path: "/invoicing", icon: InvoicingIcon, key: "invoicing" },
  { path: "/accounting", icon: AccountingIcon, key: "accounting" },
  { path: "/migration", icon: MigrationIcon, key: "import" }
] as const;

export function WorkspaceHomePage(): React.JSX.Element {
  const { t } = useTranslation("workspace");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <PageHeader
        title={t("title")}
        subtitle={t("description")}
      />
      <div>
        <h2 style={{ fontSize: "1.0625rem", fontWeight: 700, margin: "0 0 1rem 0", color: "var(--color-content-primary)" }}>
          {t("launchpad")}
        </h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" }}>
          {cards.map(({ path, icon: Icon, key }) => {
            return (
              <Link key={path} to={path} style={launchpadCardStyle}>
                <span style={cardIconContainerStyle}><Icon size={22} /></span>
                <span style={{ flex: 1, textAlign: "start" }}>
                  <strong style={{ display: "block", fontSize: "0.9375rem", color: "var(--color-content-primary)" }}>{t(`cards.${key}.title`)}</strong>
                  <span style={{ display: "block", marginTop: "0.25rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t(`cards.${key}.description`)}</span>
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
