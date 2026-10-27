import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { InvoicingIcon, RefreshIcon } from "../../../components/primitives/icons.tsx";
import { InvoiceStatusGroup } from "../components/invoice-status-pill.tsx";
import {
  useInvoicingScope,
  useInvoicingPermissions,
  useCustomerInvoices,
  useSupplierInvoices
} from "../hooks/invoice-queries.ts";
import type { InvoiceListItem, InvoiceDirection } from "../types.ts";

export interface InvoiceListPageProps {
  readonly initialTab?: "customer" | "supplier" | undefined;
}

export function InvoiceListPage({ initialTab }: InvoiceListPageProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const navigate = useNavigate();
  const { legalEntityId, branchId } = useInvoicingScope();
  const perms = useInvoicingPermissions();

  // Determine active tab based on permissions and initialTab
  const [activeTab, setActiveTab] = useState<"customer" | "supplier">(() => {
    if (initialTab) return initialTab;
    if (!perms.canReadCustomerInvoices && perms.canReadSupplierInvoices) return "supplier";
    return "customer";
  });

  const [search, setSearch] = useState("");
  const [lifecycle, setLifecycle] = useState<string>("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const isCustomer = activeTab === "customer";

  const customerQuery = useCustomerInvoices(
    {
      legalEntityId,
      branchId: branchId || undefined,
      search: search || undefined,
      lifecycle: lifecycle || undefined,
      page,
      pageSize
    },
    isCustomer && perms.canReadCustomerInvoices && Boolean(legalEntityId)
  );

  const supplierQuery = useSupplierInvoices(
    {
      legalEntityId,
      branchId: branchId || undefined,
      search: search || undefined,
      lifecycle: lifecycle || undefined,
      page,
      pageSize
    },
    !isCustomer && perms.canReadSupplierInvoices && Boolean(legalEntityId)
  );

  const currentQuery = isCustomer ? customerQuery : supplierQuery;
  const data = currentQuery.data;
  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const handleRowClick = (item: InvoiceListItem) => {
    const routePrefix = isCustomer ? "/invoicing/customer-invoices" : "/invoicing/supplier-invoices";
    if (item.lifecycle === "Draft") {
      navigate(`${routePrefix}/${encodeURIComponent(item.id)}/edit`);
    } else {
      navigate(`${routePrefix}/${encodeURIComponent(item.id)}`);
    }
  };

  const handleCreateNew = () => {
    if (isCustomer) {
      navigate("/invoicing/customer-invoices/new");
    } else {
      navigate("/invoicing/supplier-invoices/new");
    }
  };

  const activeFilters = [Boolean(search), Boolean(lifecycle)].filter(Boolean).length;

  const filterBar = (
    <FilterBar
      activeFilterCount={activeFilters}
      searchInput={
        <SearchField
          size="compact"
          value={search}
          onChange={(val) => {
            setSearch(val);
            setPage(1);
          }}
          ariaLabel={t("list.searchLabel")}
          placeholder={t("list.searchPlaceholder")}
        />
      }
      onResetFilters={() => {
        setSearch("");
        setLifecycle("");
        setPage(1);
      }}
    >
      <div className="ui-filter-item">
        <Select
          size="compact"
          aria-label={t("list.columnLifecycle")}
          selectedKey={lifecycle}
          onSelectionChange={(key) => {
            setLifecycle(String(key ?? ""));
            setPage(1);
          }}
          placeholder={t("list.allLifecycles")}
        >
          <SelectItem id="">{t("list.allLifecycles")}</SelectItem>
          <SelectItem id="Draft">{t("list.draft")}</SelectItem>
          <SelectItem id="Finalized">{t("list.finalized")}</SelectItem>
          <SelectItem id="Cancelled">{t("list.cancelled")}</SelectItem>
        </Select>
      </div>
    </FilterBar>
  );

  const canCreate = isCustomer ? perms.canCreateCustomerInvoice : perms.canCreateSupplierInvoice;
  const primaryAction = canCreate
    ? {
        label: isCustomer ? t("list.newCustomerInvoice") : t("list.newSupplierInvoice"),
        onClick: handleCreateNew
      }
    : undefined;

  return (
    <ListPage
      title={isCustomer ? t("list.customerTitle") : t("list.supplierTitle")}
      subtitle={isCustomer ? t("list.customerSubtitle") : t("list.supplierSubtitle")}
      breadcrumbs={[
        { label: t("title"), href: "/invoicing" },
        { label: isCustomer ? t("tabs.customerInvoices") : t("tabs.supplierInvoices") }
      ]}
      primaryAction={primaryAction}
      secondaryActions={
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          {/* Tabs Switcher */}
          {perms.canReadCustomerInvoices && perms.canReadSupplierInvoices && (
            <div
              style={{
                display: "inline-flex",
                backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                padding: "0.25rem",
                borderRadius: "var(--radius-md, 0.375rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                gap: "0.25rem"
              }}
            >
              <button
                type="button"
                onClick={() => {
                  setActiveTab("customer");
                  setPage(1);
                }}
                style={{
                  border: "none",
                  padding: "0.375rem 0.75rem",
                  borderRadius: "var(--radius-sm, 0.25rem)",
                  fontSize: "0.8125rem",
                  fontWeight: isCustomer ? 700 : 500,
                  backgroundColor: isCustomer ? "var(--color-surface-card, #ffffff)" : "transparent",
                  color: isCustomer ? "#102a43" : "var(--color-content-secondary, #475569)",
                  boxShadow: isCustomer ? "0 1px 2px 0 rgba(0, 0, 0, 0.05)" : "none",
                  cursor: "pointer"
                }}
              >
                {t("tabs.customerInvoices")}
              </button>
              <button
                type="button"
                onClick={() => {
                  setActiveTab("supplier");
                  setPage(1);
                }}
                style={{
                  border: "none",
                  padding: "0.375rem 0.75rem",
                  borderRadius: "var(--radius-sm, 0.25rem)",
                  fontSize: "0.8125rem",
                  fontWeight: !isCustomer ? 700 : 500,
                  backgroundColor: !isCustomer ? "var(--color-surface-card, #ffffff)" : "transparent",
                  color: !isCustomer ? "#102a43" : "var(--color-content-secondary, #475569)",
                  boxShadow: !isCustomer ? "0 1px 2px 0 rgba(0, 0, 0, 0.05)" : "none",
                  cursor: "pointer"
                }}
              >
                {t("tabs.supplierInvoices")}
              </button>
            </div>
          )}

          <Button
            variant="ghost"
            size="compact"
            onPress={() => void currentQuery.refetch()}
            aria-label={t("list.refresh")}
          >
            <RefreshIcon size={16} />
          </Button>
        </div>
      }
      filterBar={filterBar}
      pagination={
        totalCount > 0 ? (
          <Pagination
            page={page}
            totalPages={totalPages}
            totalItems={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
          />
        ) : undefined
      }
    >
      {currentQuery.isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-secondary)" }}>
          <p>{t("document.loading")}</p>
        </div>
      ) : currentQuery.error ? (
        <ErrorState
          title={t("document.loadError")}
          message={t("errors.generic")}
          onRetry={() => void currentQuery.refetch()}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={<InvoicingIcon size={36} />}
          title={
            search || lifecycle
              ? t("list.noResultsTitle")
              : isCustomer
                ? t("list.emptyCustomerTitle")
                : t("list.emptySupplierTitle")
          }
          description={
            search || lifecycle
              ? t("list.noResultsDescription")
              : isCustomer
                ? t("list.emptyCustomerDescription")
                : t("list.emptySupplierDescription")
          }
          action={
            primaryAction ? (
              <Button variant="primary" onPress={primaryAction.onClick}>
                {primaryAction.label}
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div
          style={{
            backgroundColor: "var(--color-surface-card, #ffffff)",
            border: "1px solid var(--color-border-default, #cbd5e1)",
            borderRadius: "var(--radius-lg, 0.75rem)",
            overflow: "hidden",
            boxShadow: "0 1px 3px 0 rgba(15, 23, 42, 0.06)"
          }}
        >
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
              <thead>
                <tr style={{ backgroundColor: "#102a43", color: "#ffffff", borderBottom: "1px solid #0c1f31" }}>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start", fontWeight: 700 }}>
                    {t("list.columnDocumentNumber")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start", fontWeight: 700 }}>
                    {t("list.columnParty")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start", fontWeight: 700 }}>
                    {t("list.columnDocumentDate")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "start", fontWeight: 700 }}>
                    {t("list.columnDueDate")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                    {t("list.columnTotal")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center", fontWeight: 700 }}>
                    {t("list.columnLifecycle")}
                  </th>
                  <th style={{ padding: "0.75rem 1rem", textAlign: "center", fontWeight: 700 }}>
                    {t("list.columnActions")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.map((item, index) => (
                  <tr
                    key={item.id}
                    onClick={() => handleRowClick(item)}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                      backgroundColor: index % 2 === 0 ? "#ffffff" : "var(--color-surface-subtle, #f8fafc)",
                      cursor: "pointer",
                      transition: "background-color 150ms ease"
                    }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "#f1f5f9";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = index % 2 === 0 ? "#ffffff" : "var(--color-surface-subtle, #f8fafc)";
                    }}
                  >
                    {/* Document Number */}
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "var(--color-brand-primary, #4f46e5)" }}>
                      <bdi>{item.documentNumber || t("draft.draftFallback", { id: item.id.slice(0, 8) })}</bdi>
                    </td>

                    {/* Party */}
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {item.partyName || "—"}
                    </td>

                    {/* Document Date */}
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-secondary, #475569)" }}>
                      <LocalDateDisplay value={item.documentDate} />
                    </td>

                    {/* Due Date */}
                    <td style={{ padding: "0.75rem 1rem", color: "var(--color-content-secondary, #475569)" }}>
                      {item.dueDate ? <LocalDateDisplay value={item.dueDate} /> : "—"}
                    </td>

                    {/* Total */}
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 800, color: "#102a43" }}>
                      {item.total ? (
                        <MoneyDisplay value={{ amount: item.total, currency: item.currencyCode }} />
                      ) : (
                        "—"
                      )}
                    </td>

                    {/* Status Group */}
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <InvoiceStatusGroup
                        lifecycle={item.lifecycle}
                        documentType={item.documentType}
                        settlementSummary={item.settlementSummary}
                        regulatorySummary={item.regulatorySummary}
                      />
                    </td>

                    {/* Actions */}
                    <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleRowClick(item);
                        }}
                        style={{
                          background: "none",
                          border: "1px solid #cbd5e1",
                          borderRadius: "var(--radius-sm, 0.25rem)",
                          padding: "0.25rem 0.5rem",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          color: "#1e3a5f",
                          cursor: "pointer"
                        }}
                      >
                        {item.lifecycle === "Draft" ? t("list.editDraft") : t("list.viewDetail")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </ListPage>
  );
}
