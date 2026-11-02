import React, { useState } from "react";
import { useAccountingI18n } from "../../i18n/index.ts";
import { useReconciliationsPage, useReconciliationIssuesPage, useRunReconciliationMutation } from "../../hooks/use-accounting-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { DataGrid, type DataGridColumn } from "../../../../components/grid/index.ts";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import type { SubledgerReconciliationView, ReconciliationIssueView } from "../../types.ts";
import { formatHealthCode, formatSeverity, formatStatus, formatDirection } from "../../utils/diagnostic-translations.ts";

export interface ControlReconciliationsViewProps { readonly legalEntityId?: string; }

export const ControlReconciliationsView: React.FC<ControlReconciliationsViewProps> = ({ legalEntityId }) => {
  const { t } = useAccountingI18n();
  const [isRunModalOpen, setIsRunModalOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<"AR" | "AP" | "Inventory">("AR");
  const [asOfDate, setAsOfDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [runTypeFilter, setRunTypeFilter] = useState("all");
  const [issueStatusFilter, setIssueStatusFilter] = useState("all");
  const [runPage, setRunPage] = useState(1);
  const [issuePage, setIssuePage] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const runsQuery = useReconciliationsPage({ page: runPage, limit: pageSize, type: runTypeFilter === "all" ? undefined : runTypeFilter }, legalEntityId);
  const issuesQuery = useReconciliationIssuesPage({ page: issuePage, limit: pageSize, status: issueStatusFilter === "all" ? undefined : issueStatusFilter }, legalEntityId);
  const runMutation = useRunReconciliationMutation(legalEntityId);
  const reconciliations = runsQuery.data?.items ?? [];
  const issues = issuesQuery.data?.items ?? [];

  const resetPages = (size = pageSize) => {
    setPageSize(size);
    setRunPage(1);
    setIssuePage(1);
  };

  const runColumns: readonly DataGridColumn<SubledgerReconciliationView>[] = [
    { id: "type", header: t("reconciliation.colType"), sortable: false, filterable: false, cellRenderer: ({ row }) => <strong>{formatDirection(row.type)}</strong> },
    { id: "asOfDate", header: t("reconciliation.colAsOfDate"), sortable: false, filterable: false, cellRenderer: ({ row }) => <bdi>{row.asOfDate}</bdi> },
    { id: "sourceBalance", header: t("reconciliation.colSourceBalance"), align: "end", sortable: false, filterable: false, cellRenderer: ({ row }) => row.sourceBalance == null ? "—" : <MoneyDisplay value={{ amount: row.sourceBalance.toFixed(2), currency: row.currencyCode ?? "EGP" }} /> },
    { id: "ledgerBalance", header: t("reconciliation.colLedgerBalance"), align: "end", sortable: false, filterable: false, cellRenderer: ({ row }) => row.ledgerBalance == null ? "—" : <MoneyDisplay value={{ amount: row.ledgerBalance.toFixed(2), currency: row.currencyCode ?? "EGP" }} /> },
    { id: "difference", header: t("reconciliation.colDifference"), align: "end", sortable: false, filterable: false, cellRenderer: ({ row }) => row.difference == null ? "—" : <MoneyDisplay value={{ amount: row.difference.toFixed(2), currency: row.currencyCode ?? "EGP" }} coloredSign /> },
    { id: "status", header: t("reconciliation.colStatus"), sortable: false, filterable: false, cellRenderer: ({ row }) => row.status === "Reconciled" ? <StatusBadge intent="success" label={formatStatus(row.status)} size="small" /> : row.status === "Difference" ? <StatusBadge intent="danger" label={formatStatus(row.status)} size="small" /> : <StatusBadge intent="warning" label={formatStatus(row.status)} size="small" /> },
    { id: "diagnostics", header: t("reconciliation.colDiagnostics"), sortable: false, filterable: false, cellRenderer: ({ row }) => row.diagnostics?.length ? <div style={{ display: "flex", flexWrap: "wrap", gap: "0.25rem" }}>{row.diagnostics.map((code) => <span key={code}>{formatHealthCode(code)}</span>)}</div> : "—" }
  ];

  const issueColumns: readonly DataGridColumn<ReconciliationIssueView>[] = [
    { id: "code", header: t("reconciliation.issueColCode"), sortable: false, filterable: false, cellRenderer: ({ row }) => <strong>{formatHealthCode(row.code)}</strong> },
    { id: "severity", header: t("reconciliation.issueColSeverity"), sortable: false, filterable: false, cellRenderer: ({ row }) => <StatusBadge intent={row.severity === "Blocking" ? "danger" : row.severity === "Attention" ? "warning" : "info"} label={formatSeverity(row.severity)} size="small" /> },
    { id: "status", header: t("reconciliation.issueColStatus"), sortable: false, filterable: false, cellRenderer: ({ row }) => <StatusBadge intent={row.status === "Resolved" ? "success" : "danger"} label={formatStatus(row.status)} size="small" /> },
    { id: "createdAt", header: t("reconciliation.issueColCreatedAt"), sortable: false, filterable: false, cellRenderer: ({ row }) => row.createdAt.slice(0, 10) },
    { id: "resolvedAt", header: t("reconciliation.issueColResolvedAt"), sortable: false, filterable: false, cellRenderer: ({ row }) => row.resolvedAt?.slice(0, 10) ?? "—" }
  ];

  const executeRun = async (event: React.FormEvent) => {
    event.preventDefault();
    await runMutation.mutateAsync({ type: selectedType, asOf: asOfDate });
    setIsRunModalOpen(false);
    void runsQuery.refetch();
    void issuesQuery.refetch();
  };

  const openRunModal = (type: "AR" | "AP" | "Inventory") => { setSelectedType(type); setIsRunModalOpen(true); };
  const actionStyle: React.CSSProperties = { backgroundColor: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-lg)", boxShadow: "var(--shadow-sm)", padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1rem" };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", width: "100%", minWidth: 0 }} data-testid="control-reconciliations-view">
      <Surface variant="card" style={{ padding: "1.25rem 1.5rem" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "1rem" }}>
          <div><h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>{t("reconciliation.title")}</h2><p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)" }}>{t("reconciliation.subtitle")}</p></div>
          <Button variant="secondary" size="compact" onPress={() => void runsQuery.refetch()}>{t("reconciliation.refresh")}</Button>
        </div>
      </Surface>

      <FilterBar
        activeFilterCount={(runTypeFilter !== "all" ? 1 : 0) + (issueStatusFilter !== "all" ? 1 : 0)}
        onResetFilters={() => { setRunTypeFilter("all"); setIssueStatusFilter("all"); resetPages(); }}
        isPending={runsQuery.isFetching || issuesQuery.isFetching}
      >
        <label><span style={{ display: "block", fontSize: "0.75rem", fontWeight: 600 }}>{t("reconciliation.colType")}</span><select value={runTypeFilter} onChange={(event) => { setRunTypeFilter(event.target.value); resetPages(); }}><option value="all">{t("reconciliation.allTypes")}</option><option value="AR">{t("reconciliation.runAr")}</option><option value="AP">{t("reconciliation.runAp")}</option><option value="Inventory">{t("reconciliation.runInventory")}</option></select></label>
        <label><span style={{ display: "block", fontSize: "0.75rem", fontWeight: 600 }}>{t("reconciliation.issueColStatus")}</span><select value={issueStatusFilter} onChange={(event) => { setIssueStatusFilter(event.target.value); resetPages(); }}><option value="all">{t("reconciliation.allIssueStatuses")}</option><option value="Open">{t("reconciliation.openIssues")}</option><option value="Resolved">{t("reconciliation.resolvedIssues")}</option></select></label>
      </FilterBar>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1.25rem" }}>
        {(["AR", "AP", "Inventory"] as const).map((type) => (
          <div key={type} style={actionStyle}>
            <div><strong>{t(type === "AR" ? "reconciliation.runAr" : type === "AP" ? "reconciliation.runAp" : "reconciliation.runInventory")}</strong><p style={{ margin: "0.5rem 0 0", color: "var(--color-content-muted)", fontSize: "0.8125rem" }}>{t(type === "AR" ? "reconciliation.arDescription" : type === "AP" ? "reconciliation.apDescription" : "reconciliation.inventoryDescription")}</p></div>
            <Button variant="primary" onPress={() => openRunModal(type)}>{t(type === "AR" ? "reconciliation.runArAction" : type === "AP" ? "reconciliation.runApAction" : "reconciliation.runInventoryAction")}</Button>
          </div>
        ))}
      </div>

      <Surface variant="card" title={t("reconciliation.runsHistoryTitle")} style={{ padding: "1rem" }}>
        <DataGrid columns={runColumns} staticRows={reconciliations} loading={runsQuery.isLoading} emptyMessage={t("reconciliation.noRunsYet")} density="compact" />
        <Pagination page={runPage} totalItems={runsQuery.data?.totalCount ?? 0} totalPages={Math.max(1, Math.ceil((runsQuery.data?.totalCount ?? 0) / pageSize))} pageSize={pageSize} onPageChange={setRunPage} onPageSizeChange={resetPages} isDisabled={runsQuery.isFetching} />
      </Surface>

      <Surface variant="card" title={t("reconciliation.issuesTitle")} description={t("reconciliation.issuesSubtitle")} style={{ padding: "1rem" }}>
        <DataGrid columns={issueColumns} staticRows={issues} loading={issuesQuery.isLoading} emptyMessage={t("reconciliation.noIssuesFound")} density="compact" />
        <Pagination page={issuePage} totalItems={issuesQuery.data?.totalCount ?? 0} totalPages={Math.max(1, Math.ceil((issuesQuery.data?.totalCount ?? 0) / pageSize))} pageSize={pageSize} onPageChange={setIssuePage} onPageSizeChange={resetPages} isDisabled={issuesQuery.isFetching} />
      </Surface>

      <Dialog isOpen={isRunModalOpen} onOpenChange={(open) => !open && setIsRunModalOpen(false)} title={`${t("reconciliation.modalRunTitle")} — ${selectedType}`} size="medium" footer={<div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}><Button variant="secondary" onPress={() => setIsRunModalOpen(false)}>{t("common.cancel")}</Button><Button variant="primary" isLoading={runMutation.isPending} onPress={() => void executeRun({ preventDefault: () => undefined } as React.FormEvent)}>{t("reconciliation.modalSubmit")}</Button></div>}>
        <form onSubmit={executeRun} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <label htmlFor="rec-as-of">{t("reconciliation.modalAsOfLabel")}</label>
          <input id="rec-as-of" type="date" value={asOfDate} onChange={(event) => setAsOfDate(event.target.value)} required style={{ padding: "0.5rem 0.75rem", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }} />
          {runMutation.isError && <p role="alert" style={{ color: "var(--color-status-danger)" }}>{t("reconciliation.modalRunError")}</p>}
        </form>
      </Dialog>
    </div>
  );
};
