import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useLeads } from "../../hooks/use-crm-queries.ts";
import { crmApi } from "../../services/crm-api.ts";
import type { Lead } from "../../types.ts";
import { PriorityBadge } from "../../components/priority-badge.tsx";
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { LocalDateField } from "../../../../components/fields/local-date-field.tsx";
import { NewLeadModal } from "./new-lead-modal.tsx";
import { EditLeadModal } from "./edit-lead-modal.tsx";
import { AssignLeadModal } from "./assign-lead-modal.tsx";
import { QualifyLeadModal } from "./qualify-lead-modal.tsx";
import { DisqualifyLeadModal } from "./disqualify-lead-modal.tsx";
import { ConvertLeadModal } from "./convert-lead-modal.tsx";
import { MergeLeadsModal } from "./merge-leads-modal.tsx";
import { ImportLeadsModal } from "./import-leads-modal.tsx";

export interface CrmLeadsTabProps {
  readonly legalEntityId: string;
  readonly canManage?: boolean;
}

export function CrmLeadsTab({ legalEntityId, canManage }: CrmLeadsTabProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [scope, setScope] = useState<"mine" | "all">("mine");
  const [page, setPage] = useState(1);
  const pageSize = 15;

  // Active Modals state
  const [isNewModalOpen, setIsNewModalOpen] = useState(false);
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [activeEditLead, setActiveEditLead] = useState<Lead | null>(null);
  const [activeAssignLead, setActiveAssignLead] = useState<Lead | null>(null);
  const [activeQualifyLead, setActiveQualifyLead] = useState<Lead | null>(null);
  const [activeDisqualifyLead, setActiveDisqualifyLead] = useState<Lead | null>(null);
  const [activeConvertLead, setActiveConvertLead] = useState<Lead | null>(null);
  const [activeMergeLead, setActiveMergeLead] = useState<Lead | null>(null);

  const leadsQuery = useLeads(legalEntityId, {
    status: statusFilter || undefined,
    search: search.trim() || undefined,
    scope,
    page,
    pageSize
  });

  const leadPage = leadsQuery.data;
  const items: readonly Lead[] = leadPage?.items ?? [];
  const totalCount = leadPage?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const handleExportCsv = async () => {
    try {
      const csv = await crmApi.exportLeads(legalEntityId, {
        status: statusFilter || undefined,
        search: search.trim() || undefined
      });
      const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.setAttribute("download", `crm-leads-${new Date().toISOString().slice(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch {
      // ignore
    }
  };

  const statusTabs = [
    { id: "", label: t("leads.statusFilter.all") },
    { id: "New", label: t("leads.statusFilter.new") },
    { id: "Working", label: t("leads.statusFilter.working") },
    { id: "Qualified", label: t("leads.statusFilter.qualified") },
    { id: "Disqualified", label: t("leads.statusFilter.disqualified") },
    { id: "Converted", label: t("leads.statusFilter.converted") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Status Sub-Navigation Pills */}
      <div style={{ display: "flex", gap: "0.5rem", overflowX: "auto", borderBottom: "1px solid var(--color-border-default)", paddingBottom: "0.75rem" }}>
        {statusTabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => {
              setStatusFilter(tab.id);
              setPage(1);
            }}
            style={{
              padding: "0.375rem 0.875rem",
              borderRadius: "9999px",
              border: "1px solid",
              borderColor: statusFilter === tab.id ? "var(--color-action-primary)" : "var(--color-border-default)",
              backgroundColor: statusFilter === tab.id ? "var(--color-action-primary)" : "var(--color-surface-card)",
              color: statusFilter === tab.id ? "#ffffff" : "var(--color-content-primary)",
              fontWeight: statusFilter === tab.id ? 700 : 500,
              fontSize: "0.8125rem",
              cursor: "pointer",
              transition: "all 0.15s ease"
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Top Filter and Actions Bar */}
      <FilterBar>
        <div style={{ display: "flex", gap: "0.75rem", flex: 1, flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ minWidth: "260px", maxWidth: "400px", flex: 1 }}>
            <SearchField
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
              placeholder={t("leads.searchPlaceholder")}
            />
          </div>

          {/* Scope Selector: My Leads vs All Leads */}
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
              {t("leads.scopeMine")}
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
              {t("leads.scopeAll")}
            </button>
          </div>
        </div>

        <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
          <Button variant="secondary" size="comfortable" onPress={() => setIsImportModalOpen(true)}>
            {t("leads.actions.importCsv")}
          </Button>
          <Button variant="secondary" size="comfortable" onPress={handleExportCsv}>
            {t("leads.actions.exportCsv")}
          </Button>
          <Button variant="primary" size="comfortable" onPress={() => setIsNewModalOpen(true)}>
            + {t("leads.actions.newLead")}
          </Button>
        </div>
      </FilterBar>

      {/* Structured Clean Data Table (Zero mixed view/form collision) */}
      <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
        <Table aria-label={t("leads.tableAria")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("leads.columns.nameAndCompany")}</TableHead>
              <TableHead>{t("leads.columns.contact")}</TableHead>
              <TableHead>{t("leads.columns.source")}</TableHead>
              <TableHead>{t("leads.columns.priority")}</TableHead>
              <TableHead>{t("leads.columns.score")}</TableHead>
              <TableHead>{t("leads.columns.status")}</TableHead>
              <TableHead>{t("leads.columns.nextAction")}</TableHead>
              <TableHead style={{ textAlign: "end" }}>{t("leads.columns.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {leadsQuery.isLoading ? (
              <TableRow>
                <TableCell colSpan={8} style={{ textAlign: "center", padding: "2.5rem" }}>
                  {t("leads.loading")}
                </TableCell>
              </TableRow>
            ) : items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} style={{ textAlign: "center", padding: "2.5rem", color: "var(--color-content-muted)" }}>
                  {t("leads.emptyState")}
                </TableCell>
              </TableRow>
            ) : (
              items.map((lead) => (
                <TableRow key={lead.id} data-testid="crm-lead-row">
                  <TableCell>
                    <div style={{ display: "flex", flexDirection: "column" }}>
                      <span style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {lead.displayName}
                      </span>
                      {lead.companyName && (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                          {lead.companyName}
                        </span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div style={{ display: "flex", flexDirection: "column", fontSize: "0.8125rem" }}>
                      {lead.phone && <span>{lead.phone}</span>}
                      {lead.email && <span style={{ color: "var(--color-content-muted)" }}>{lead.email}</span>}
                      {!lead.phone && !lead.email && <span>—</span>}
                    </div>
                  </TableCell>
                  <TableCell>{t(`leadSources.${lead.source}`, lead.source)}</TableCell>
                  <TableCell>
                    <PriorityBadge priority={lead.priority} />
                  </TableCell>
                  <TableCell>
                    <span style={{ fontWeight: 600, color: lead.score >= 70 ? "var(--color-status-success)" : "inherit" }}>
                      {lead.score}
                    </span>
                  </TableCell>
                  <TableCell>
                    <span
                      style={{
                        padding: "0.125rem 0.5rem",
                        borderRadius: "var(--radius-sm)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor:
                          lead.status === "Qualified" ? "var(--color-status-success-subtle)" :
                          lead.status === "Disqualified" ? "var(--color-status-danger-subtle)" :
                          lead.status === "Converted" ? "var(--color-status-info-subtle)" :
                          "var(--color-surface-subtle)",
                        color:
                          lead.status === "Qualified" ? "var(--color-status-success)" :
                          lead.status === "Disqualified" ? "var(--color-status-danger)" :
                          lead.status === "Converted" ? "var(--color-status-info)" :
                          "var(--color-content-primary)"
                      }}
                    >
                      {t(`leadStatuses.${lead.status}`)}
                    </span>
                  </TableCell>
                  <TableCell>
                    {lead.nextActionAt ? (
                      <LocalDateField value={lead.nextActionAt} />
                    ) : (
                      <span style={{ color: "var(--color-content-muted)" }}>—</span>
                    )}
                  </TableCell>
                  <TableCell style={{ textAlign: "end" }}>
                    <div style={{ display: "inline-flex", gap: "0.375rem", alignItems: "center" }}>
                      {/* Qualification Button */}
                      {(lead.status === "New" || lead.status === "Working") && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onPress={() => setActiveQualifyLead(lead)}
                        >
                          {t("leads.actions.qualify")}
                        </Button>
                      )}

                      {/* Convert to Opportunity Button */}
                      {lead.status === "Qualified" && (
                        <Button
                          size="compact"
                          variant="primary"
                          onPress={() => setActiveConvertLead(lead)}
                        >
                          {t("leads.actions.convert")}
                        </Button>
                      )}

                      {/* Disqualify Button */}
                      {(lead.status === "New" || lead.status === "Working") && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onPress={() => setActiveDisqualifyLead(lead)}
                        >
                          {t("leads.actions.disqualify")}
                        </Button>
                      )}

                      {/* Merge Candidates */}
                      {lead.duplicateCandidateIds && lead.duplicateCandidateIds.length > 0 && (
                        <Button
                          size="compact"
                          variant="secondary"
                          onPress={() => setActiveMergeLead(lead)}
                          style={{ color: "var(--color-status-warning)" }}
                        >
                          {t("leads.actions.merge")}
                        </Button>
                      )}

                      {/* Assign Button */}
                      <Button
                        size="compact"
                        variant="secondary"
                        onPress={() => setActiveAssignLead(lead)}
                      >
                        {t("leads.actions.assign")}
                      </Button>

                      {/* Edit Button */}
                      <Button
                        size="compact"
                        variant="secondary"
                        onPress={() => setActiveEditLead(lead)}
                      >
                        {t("actions.edit")}
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

      {/* Modals */}
      <NewLeadModal
        isOpen={isNewModalOpen}
        onClose={() => setIsNewModalOpen(false)}
        legalEntityId={legalEntityId}
      />

      <EditLeadModal
        isOpen={Boolean(activeEditLead)}
        onClose={() => setActiveEditLead(null)}
        legalEntityId={legalEntityId}
        lead={activeEditLead}
      />

      <AssignLeadModal
        isOpen={Boolean(activeAssignLead)}
        onClose={() => setActiveAssignLead(null)}
        legalEntityId={legalEntityId}
        lead={activeAssignLead}
      />

      <QualifyLeadModal
        isOpen={Boolean(activeQualifyLead)}
        onClose={() => setActiveQualifyLead(null)}
        legalEntityId={legalEntityId}
        lead={activeQualifyLead}
      />

      <DisqualifyLeadModal
        isOpen={Boolean(activeDisqualifyLead)}
        onClose={() => setActiveDisqualifyLead(null)}
        legalEntityId={legalEntityId}
        lead={activeDisqualifyLead}
      />

      <ConvertLeadModal
        isOpen={Boolean(activeConvertLead)}
        onClose={() => setActiveConvertLead(null)}
        legalEntityId={legalEntityId}
        lead={activeConvertLead}
      />

      <MergeLeadsModal
        isOpen={Boolean(activeMergeLead)}
        onClose={() => setActiveMergeLead(null)}
        legalEntityId={legalEntityId}
        lead={activeMergeLead}
      />

      <ImportLeadsModal
        isOpen={isImportModalOpen}
        onClose={() => setIsImportModalOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
