import React, { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router-dom";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { FinancialStatusBadge } from "../components/financial-status-badge.tsx";
import { OpenItemDetailDrawer } from "../components/open-item-detail-drawer.tsx";
import { WriteOffDialog } from "../components/write-off-dialog.tsx";
import { ReversalDialog } from "../components/reversal-dialog.tsx";
import { useSubledgerScope, useOpenItemsQuery } from "../hooks/subledger-queries.ts";
import type { OpenItemListFilter, PartyOpenItem, SubledgerDirection } from "../types.ts";

export function OpenItemsPage(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const navigate = useNavigate();
  const { legalEntityId } = useSubledgerScope();
  const [searchTerm, setSearchTerm] = useState("");
  const [directionFilter, setDirectionFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("openOnly");
  const [currencyFilter, setCurrencyFilter] = useState("");
  const [sort, setSort] = useState<"idAsc" | "dueDateAsc" | "dueDateDesc">("idAsc");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [selectedDetailItem, setSelectedDetailItem] = useState<PartyOpenItem | null>(null);
  const [selectedWriteOffItem, setSelectedWriteOffItem] = useState<PartyOpenItem | null>(null);
  const [selectedReversal, setSelectedReversal] = useState<{ applicationId: string; openItemId: string } | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const resetCursor = () => setPage(1);
  const resetFilters = () => { setSearchTerm(""); setDirectionFilter("all"); setStatusFilter("openOnly"); setCurrencyFilter(""); setSort("idAsc"); resetCursor(); };

  const queryFilter = useMemo<OpenItemListFilter>(() => {
    const extra: { direction?: SubledgerDirection; openOnly?: boolean; overdueOnly?: boolean; asOfDate?: string; currencyCode?: string; search?: string } = {};
    if (directionFilter === "receivable" || directionFilter === "payable") extra.direction = directionFilter === "receivable" ? "Receivable" : "Payable";
    if (statusFilter === "openOnly") extra.openOnly = true;
    if (statusFilter === "overdueOnly") { extra.openOnly = true; extra.overdueOnly = true; extra.asOfDate = new Date().toISOString().slice(0, 10); }
    if (statusFilter === "all") extra.openOnly = false;
    if (currencyFilter) extra.currencyCode = currencyFilter;
    if (searchTerm.trim()) extra.search = searchTerm.trim();
    return { legalEntityId, limit: pageSize, page, sort, ...extra };
  }, [legalEntityId, directionFilter, statusFilter, currencyFilter, searchTerm, page, pageSize, sort]);

  const openItemsQuery = useOpenItemsQuery(queryFilter);
  const items = openItemsQuery.data?.items ?? [];
  const isLoading = openItemsQuery.isLoading;
  const activeFilterCount = (searchTerm ? 1 : 0) + (directionFilter !== "all" ? 1 : 0) + (statusFilter !== "openOnly" ? 1 : 0) + (currencyFilter ? 1 : 0) + (sort !== "idAsc" ? 1 : 0);

  const columns: readonly DataGridColumn<PartyOpenItem>[] = [
    { id: "document", header: t("openItems.documentRef"), minWidth: 180, sortable: false, filterable: false, cellRenderer: ({ row }) => <div><strong>{row.sourceDisplayNumber || row.sourceReference}</strong><small style={{ display: "block", color: "var(--color-content-muted)" }}>{row.sourceReference}</small></div> },
    { id: "sourceType", header: t("drawer.sourceType"), minWidth: 130, sortable: false, filterable: false, cellRenderer: ({ row }) => <div><strong>{row.sourceType}</strong><small style={{ display: "block", color: "var(--color-content-muted)" }}>{row.polarity === "Debit" ? t("common.debit") : t("common.credit")}</small></div> },
    { id: "documentDate", header: t("openItems.documentDate"), width: 120, sortable: false, filterable: false, cellRenderer: ({ row }) => row.documentDate },
    { id: "dueDate", header: t("openItems.dueDate"), width: 130, sortable: false, filterable: false, cellRenderer: ({ row }) => { const overdue = new Date(row.dueDate) < new Date() && Number(row.openTransactionAmount) > 0; return <span style={{ color: overdue ? "var(--color-status-danger)" : undefined, fontWeight: overdue ? 700 : undefined }}>{row.dueDate}</span>; } },
    { id: "originalAmount", header: t("openItems.originalAmount"), align: "end", width: 150, sortable: false, filterable: false, cellRenderer: ({ row }) => <MoneyDisplay value={{ amount: row.originalTransactionAmount, currency: row.transactionCurrency }} /> },
    { id: "settledAmount", header: t("openItems.settledAmount"), align: "end", width: 150, sortable: false, filterable: false, cellRenderer: ({ row }) => <MoneyDisplay value={{ amount: row.settledTransactionAmount, currency: row.transactionCurrency }} /> },
    { id: "remainingAmount", header: t("openItems.remainingAmount"), align: "end", width: 160, sortable: false, filterable: false, cellRenderer: ({ row }) => <strong><MoneyDisplay value={{ amount: row.openTransactionAmount, currency: row.transactionCurrency }} /></strong> },
    { id: "status", header: t("openItems.status"), width: 130, sortable: false, filterable: false, cellRenderer: ({ row }) => <FinancialStatusBadge status={row.status} isOverdue={new Date(row.dueDate) < new Date() && Number(row.openTransactionAmount) > 0} /> },
    { id: "actions", header: t("openItems.actions"), minWidth: 270, sortable: false, filterable: false, cellRenderer: ({ row }) => { const settled = Number(row.openTransactionAmount) <= 0; const hasSettlement = Number(row.settledTransactionAmount) > 0; return <div style={{ display: "flex", gap: "0.35rem", flexWrap: "wrap" }}><Button size="compact" variant="secondary" onClick={() => setSelectedDetailItem(row)}>{t("openItems.viewDetails")}</Button>{!settled && <Button size="compact" variant="secondary" onClick={() => navigate(`/subledger/applications?partyId=${row.partyId}&direction=${row.direction}&sourceId=${row.id}`)}>{t("openItems.applyNow")}</Button>}{!settled && <Button size="compact" variant="danger" onClick={() => setSelectedWriteOffItem(row)}>{t("openItems.writeOff")}</Button>}{hasSettlement && <Button size="compact" variant="secondary" onClick={() => setSelectedReversal({ applicationId: `app-${row.id}`, openItemId: row.id })}>{t("reversal.title")}</Button>}</div>; } }
  ];

  const pagination = <Pagination page={page} totalItems={openItemsQuery.data?.totalCount ?? 0} totalPages={Math.max(1, Math.ceil((openItemsQuery.data?.totalCount ?? 0) / pageSize))} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(size) => { setPageSize(size); setPage(1); }} isDisabled={openItemsQuery.isFetching} />;
  const filterBar = <FilterBar searchInput={<SearchField value={searchTerm} onChange={(value) => { setSearchTerm(value); resetCursor(); }} placeholder={t("openItems.searchPlaceholder")} isPending={openItemsQuery.isFetching} ariaLabel={t("openItemsPage.searchLabel")} />} activeFilterCount={activeFilterCount} onResetFilters={resetFilters} isPending={openItemsQuery.isFetching}>
    <label><span style={{ display: "block", fontSize: "0.75rem", fontWeight: 600 }}>{t("openItems.filterDirection")}</span><select value={directionFilter} onChange={(event) => { setDirectionFilter(event.target.value); resetCursor(); }}><option value="all">{t("openItems.allDirections")}</option><option value="receivable">{t("openItems.receivableOnly")}</option><option value="payable">{t("openItems.payableOnly")}</option></select></label>
    <label><span style={{ display: "block", fontSize: "0.75rem", fontWeight: 600 }}>{t("openItems.filterStatus")}</span><select value={statusFilter} onChange={(event) => { setStatusFilter(event.target.value); resetCursor(); }}><option value="openOnly">{t("openItems.openOnly")}</option><option value="overdueOnly">{t("openItems.overdueOnly")}</option><option value="all">{t("openItems.allStatuses")}</option></select></label>
    <label><span style={{ display: "block", fontSize: "0.75rem", fontWeight: 600 }}>{t("openItems.sortBy")}</span><select value={sort} onChange={(event) => { setSort(event.target.value as "idAsc" | "dueDateAsc" | "dueDateDesc"); resetCursor(); }}><option value="idAsc">{t("openItems.recordOrder")}</option><option value="dueDateAsc">{t("openItems.dueDateAscending")}</option><option value="dueDateDesc">{t("openItems.dueDateDescending")}</option></select></label>
    <TextField value={currencyFilter} onChange={(value) => { setCurrencyFilter(value.toUpperCase()); resetCursor(); }} placeholder={t("common.currencyPlaceholder")} aria-label={t("openItems.currency")} />
  </FilterBar>;

  return (
    <ListPage title={t("openItems.title")} subtitle={t("openItems.subtitle")} filterBar={filterBar} pagination={pagination} secondaryActions={<Link to="/subledger/applications"><Button variant="primary" size="compact">{t("application.title")}</Button></Link>}>
      {successMessage && <Alert variant="success">{successMessage}</Alert>}
      <Surface variant="card" padding="none" style={{ padding: "1rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.75rem" }}><strong>{t("openItemsPage.totalDisplayed")} {items.length}</strong><Button variant="secondary" size="compact" onPress={() => void openItemsQuery.refetch()} isDisabled={isLoading}>{t("common.refresh")}</Button></div>
        <DataGrid columns={columns} staticRows={items} loading={isLoading} emptyMessage={t("openItems.emptyDescription")} hasActiveFilters={activeFilterCount > 0} density="compact" />
      </Surface>

      {selectedDetailItem && <OpenItemDetailDrawer item={selectedDetailItem} isOpen onClose={() => setSelectedDetailItem(null)} onTriggerReversal={(applicationId) => { setSelectedReversal({ applicationId, openItemId: selectedDetailItem.id }); setSelectedDetailItem(null); }} onTriggerWriteOff={() => { setSelectedWriteOffItem(selectedDetailItem); setSelectedDetailItem(null); }} />}
      {selectedWriteOffItem && <WriteOffDialog item={selectedWriteOffItem} legalEntityId={legalEntityId} isOpen onClose={() => setSelectedWriteOffItem(null)} onCompleted={() => { setSelectedWriteOffItem(null); setSuccessMessage(t("common.saved", "تم الحفظ بنجاح")); void openItemsQuery.refetch(); }} />}
      {selectedReversal && <ReversalDialog applicationId={selectedReversal.applicationId} legalEntityId={legalEntityId} isOpen onClose={() => setSelectedReversal(null)} onCompleted={() => { setSelectedReversal(null); setSuccessMessage(t("reversal.successMessage")); void openItemsQuery.refetch(); }} />}
    </ListPage>
  );
}
