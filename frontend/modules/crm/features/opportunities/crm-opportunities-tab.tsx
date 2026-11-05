import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOpportunities, usePipelines } from "../../hooks/use-crm-queries.ts";
import type { Opportunity } from "../../types.ts";
import { CrmPipelineBoard } from "../../components/crm-pipeline-board.tsx";
import { DealHealthBadge } from "../../components/deal-health-badge.tsx";
import { ForecastCategoryBadge } from "../../components/forecast-category-badge.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { NewOpportunityModal } from "./new-opportunity-modal.tsx";
import { OpportunityWorkspaceModal } from "./opportunity-workspace-modal.tsx";
import { WinOpportunityModal } from "./win-opportunity-modal.tsx";
import { LoseOpportunityModal } from "./lose-opportunity-modal.tsx";

export interface CrmOpportunitiesTabProps {
  readonly legalEntityId: string;
  readonly canManage?: boolean;
}

export function CrmOpportunitiesTab({ legalEntityId, canManage }: CrmOpportunitiesTabProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const [viewMode, setViewMode] = useState<"board" | "list">("board");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [scope, setScope] = useState<"mine" | "all">("mine");
  const [selectedPipelineId, setSelectedPipelineId] = useState<string>("");
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Modals state
  const [isNewModalOpen, setIsNewModalOpen] = useState(false);
  const [activeWorkspaceOpp, setActiveWorkspaceOpp] = useState<Opportunity | null>(null);
  const [activeWinOpp, setActiveWinOpp] = useState<Opportunity | null>(null);
  const [activeLoseOpp, setActiveLoseOpp] = useState<Opportunity | null>(null);

  const { data: pipelines = [] } = usePipelines(legalEntityId);

  const activePipeline = pipelines.find((p) => p.id === selectedPipelineId) || pipelines.find((p) => p.isDefault) || pipelines[0];
  const stages = activePipeline?.stages || [];

  const oppsQuery = useOpportunities(legalEntityId, {
    status: statusFilter || undefined,
    pipelineId: selectedPipelineId || undefined,
    search: search.trim() || undefined,
    scope,
    page: viewMode === "list" ? page : 1,
    pageSize: viewMode === "board" ? 100 : pageSize
  });

  const oppPage = oppsQuery.data;
  const items: readonly Opportunity[] = oppPage?.items ?? [];
  const totalCount = oppPage?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const pipelineOptions = [
    { id: "", label: t("opportunities.allPipelines") },
    ...pipelines.map((p) => ({ id: p.id, label: p.name }))
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Filter and Actions Bar */}
      <FilterBar>
        <div style={{ display: "flex", gap: "0.75rem", flex: 1, flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ minWidth: "240px", maxWidth: "360px", flex: 1 }}>
            <SearchField
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
              placeholder={t("opportunities.searchPlaceholder")}
            />
          </div>

          {/* Pipeline Selector */}
          {pipelines.length > 1 && (
            <div style={{ width: "200px" }}>
              <Select
                selectedKey={selectedPipelineId}
                onSelectionChange={(k) => {
                  setSelectedPipelineId(String(k));
                  setPage(1);
                }}
                options={pipelineOptions}
                size="compact"
              />
            </div>
          )}

          {/* Scope Selector: My Deals vs All Deals */}
          <div style={{ display: "inline-flex", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
            <button
              type="button"
              onClick={() => { setScope("mine"); setPage(1); }}
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                backgroundColor: scope === "mine" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                color: scope === "mine" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
              }}
            >
              {t("opportunities.scopeMine")}
            </button>
            <button
              type="button"
              onClick={() => { setScope("all"); setPage(1); }}
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                borderInlineStart: "1px solid var(--color-border-default)",
                backgroundColor: scope === "all" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                color: scope === "all" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
              }}
            >
              {t("opportunities.scopeAll")}
            </button>
          </div>

          {/* View Mode Toggle: Board vs List */}
          <div style={{ display: "inline-flex", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
            <button
              type="button"
              onClick={() => setViewMode("board")}
              style={{
                padding: "0.375rem 0.625rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                backgroundColor: viewMode === "board" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                color: viewMode === "board" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
              }}
              title={t("opportunities.boardView")}
            >
              📊 {t("opportunities.boardView")}
            </button>
            <button
              type="button"
              onClick={() => setViewMode("list")}
              style={{
                padding: "0.375rem 0.625rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                borderInlineStart: "1px solid var(--color-border-default)",
                backgroundColor: viewMode === "list" ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                color: viewMode === "list" ? "var(--color-brand-primary)" : "var(--color-content-muted)"
              }}
              title={t("opportunities.listView")}
            >
              📋 {t("opportunities.listView")}
            </button>
          </div>
        </div>

        <Button variant="primary" size="comfortable" onPress={() => setIsNewModalOpen(true)}>
          + {t("opportunities.actions.newOpportunity")}
        </Button>
      </FilterBar>

      {/* Main View Area: Board vs List */}
      {viewMode === "board" ? (
        <CrmPipelineBoard
          stages={stages}
          opportunities={items}
          onSelectOpportunity={(opp) => setActiveWorkspaceOpp(opp)}
          onWin={(opp) => setActiveWinOpp(opp)}
          onLose={(opp) => setActiveLoseOpp(opp)}
        />
      ) : (
        <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
          <Table aria-label={t("opportunities.tableAria")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("opportunities.columns.name")}</TableHead>
                <TableHead>{t("opportunities.columns.stage")}</TableHead>
                <TableHead>{t("opportunities.columns.estimatedAmount")}</TableHead>
                <TableHead>{t("opportunities.columns.expectedCloseDate")}</TableHead>
                <TableHead>{t("opportunities.columns.forecastCategory")}</TableHead>
                <TableHead>{t("opportunities.columns.health")}</TableHead>
                <TableHead>{t("opportunities.columns.status")}</TableHead>
                <TableHead style={{ textAlign: "end" }}>{t("opportunities.columns.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {oppsQuery.isLoading ? (
                <TableRow>
                  <TableCell colSpan={8} style={{ textAlign: "center", padding: "2.5rem" }}>
                    {t("leads.loading")}
                  </TableCell>
                </TableRow>
              ) : items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} style={{ textAlign: "center", padding: "2.5rem", color: "var(--color-content-muted)" }}>
                    {t("opportunities.emptyState")}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((opp) => (
                  <TableRow key={opp.id} data-testid="crm-opportunity-row">
                    <TableCell>
                      <button
                        type="button"
                        onClick={() => setActiveWorkspaceOpp(opp)}
                        style={{
                          background: "transparent",
                          border: "none",
                          padding: 0,
                          cursor: "pointer",
                          fontWeight: 600,
                          color: "var(--color-brand-primary)",
                          textAlign: "start"
                        }}
                      >
                        {opp.name}
                      </button>
                    </TableCell>
                    <TableCell>{opp.stageName}</TableCell>
                    <TableCell>
                      <MoneyDisplay value={{ amount: String(opp.estimatedAmount), currency: opp.currencyCode }} size="small" />
                    </TableCell>
                    <TableCell>
                      {opp.expectedCloseDate ? <LocalDateField value={opp.expectedCloseDate} /> : "—"}
                    </TableCell>
                    <TableCell>
                      <ForecastCategoryBadge category={opp.forecastCategory} />
                    </TableCell>
                    <TableCell>
                      <DealHealthBadge status={opp.healthStatus} score={opp.healthScore} />
                    </TableCell>
                    <TableCell>
                      <span
                        style={{
                          padding: "0.125rem 0.5rem",
                          borderRadius: "var(--radius-sm)",
                          fontSize: "0.75rem",
                          fontWeight: 600,
                          backgroundColor:
                            opp.status === "Won" ? "var(--color-status-success-subtle)" :
                            opp.status === "Lost" ? "var(--color-status-danger-subtle)" :
                            "var(--color-surface-subtle)",
                          color:
                            opp.status === "Won" ? "var(--color-status-success)" :
                            opp.status === "Lost" ? "var(--color-status-danger)" :
                            "var(--color-content-primary)"
                        }}
                      >
                        {t(`opportunityStatuses.${opp.status}`)}
                      </span>
                    </TableCell>
                    <TableCell style={{ textAlign: "end" }}>
                      <div style={{ display: "inline-flex", gap: "0.375rem", alignItems: "center" }}>
                        <Button
                          size="compact"
                          variant="secondary"
                          onPress={() => setActiveWorkspaceOpp(opp)}
                        >
                          {t("actions.view")}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {totalPages > 1 && (
            <div style={{ padding: "0.875rem 1.25rem", borderTop: "1px solid var(--color-border-default)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                {t("common.showingRange", {
                  start: (page - 1) * pageSize + 1,
                  end: Math.min(page * pageSize, totalCount),
                  total: totalCount
                })}
              </span>
              <Pagination
                page={page}
                totalPages={totalPages}
                onPageChange={setPage}
              />
            </div>
          )}
        </div>
      )}

      {/* Modals */}
      <NewOpportunityModal
        isOpen={isNewModalOpen}
        onClose={() => setIsNewModalOpen(false)}
        legalEntityId={legalEntityId}
      />

      <OpportunityWorkspaceModal
        isOpen={Boolean(activeWorkspaceOpp)}
        onClose={() => setActiveWorkspaceOpp(null)}
        legalEntityId={legalEntityId}
        opportunity={activeWorkspaceOpp}
      />

      <WinOpportunityModal
        isOpen={Boolean(activeWinOpp)}
        onClose={() => setActiveWinOpp(null)}
        legalEntityId={legalEntityId}
        opportunity={activeWinOpp}
      />

      <LoseOpportunityModal
        isOpen={Boolean(activeLoseOpp)}
        onClose={() => setActiveLoseOpp(null)}
        legalEntityId={legalEntityId}
        opportunity={activeLoseOpp}
      />
    </div>
  );
}
