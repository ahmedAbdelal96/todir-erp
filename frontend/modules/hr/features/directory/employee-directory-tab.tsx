import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { FilterBar } from "../../../../components/primitives/filter-bar.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { useEmployees } from "../../hooks/use-hr-queries.ts";
import { NewEmployeeModal } from "./new-employee-modal.tsx";
import type { EmployeeListItem } from "../../types.ts";

export interface EmployeeDirectoryTabProps {
  readonly legalEntityId: string;
  readonly onSelectEmployee: (id: string) => void;
  readonly isNewModalOpen?: boolean;
  readonly onOpenNewModal?: () => void;
  readonly onCloseNewModal?: () => void;
}

export function EmployeeDirectoryTab({
  legalEntityId,
  onSelectEmployee,
  isNewModalOpen: controlledNewOpen,
  onOpenNewModal: controlledOnOpen,
  onCloseNewModal: controlledOnClose
}: EmployeeDirectoryTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");

  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [internalNewOpen, setInternalNewOpen] = useState(false);
  const isNewOpen = controlledNewOpen !== undefined ? controlledNewOpen : internalNewOpen;
  const openNew = controlledOnOpen ?? (() => setInternalNewOpen(true));
  const closeNew = controlledOnClose ?? (() => setInternalNewOpen(false));

  const employeesQuery = useEmployees({
    legalEntityId,
    search: search.trim() || undefined,
    page,
    pageSize
  });

  const employeePage = employeesQuery.data;
  const items: readonly EmployeeListItem[] = employeePage?.items ?? [];
  const totalCount = employeePage?.totalCount ?? 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Top Filter and Search Action Bar */}
      <FilterBar>
        <div style={{ display: "flex", gap: "0.75rem", flex: 1, flexWrap: "wrap", alignItems: "center" }}>
          <div style={{ minWidth: "280px", maxWidth: "450px", flex: 1 }}>
            <SearchField
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
              placeholder={t("directory.searchPlaceholder")}
              ariaLabel={t("directory.searchPlaceholder")}
            />
          </div>
        </div>
        <Button variant="primary" onPress={openNew}>
          + {t("directory.newEmployeeAction")}
        </Button>
      </FilterBar>

      {/* Structured Clean Data Table (Zero mixed view/form collision) */}
      <div style={{ background: "var(--color-surface-card)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
        <Table aria-label={t("directory.tableAria")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("directory.columns.number")}</TableHead>
              <TableHead>{t("directory.columns.name")}</TableHead>
              <TableHead>{t("directory.columns.englishName")}</TableHead>
              <TableHead>{t("directory.columns.startDate")}</TableHead>
              <TableHead>{t("directory.columns.status")}</TableHead>
              <TableHead style={{ textAlign: "end" }}>{t("directory.columns.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {employeesQuery.isLoading ? (
              <TableRow>
                <TableCell colSpan={6} style={{ textAlign: "center", padding: "2rem" }}>
                  {t("directory.loading")}
                </TableCell>
              </TableRow>
            ) : items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} style={{ textAlign: "center", padding: "2rem", color: "var(--color-content-muted)" }}>
                  {t("directory.emptyState")}
                </TableCell>
              </TableRow>
            ) : (
              items.map((emp) => (
                <TableRow
                  key={emp.id}
                  onClick={() => onSelectEmployee(emp.id)}
                  style={{ cursor: "pointer" }}
                >
                  <TableCell style={{ fontFamily: "monospace", fontWeight: 600 }}>
                    {emp.employmentNumber || "—"}
                  </TableCell>
                  <TableCell style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                    {emp.displayName}
                  </TableCell>
                  <TableCell style={{ color: "var(--color-content-muted)" }}>
                    {emp.englishName || "—"}
                  </TableCell>
                  <TableCell>
                    {emp.currentEmploymentStartDate ? (
                      <LocalDateDisplay value={emp.currentEmploymentStartDate} />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    <HrStatusBadge status={emp.status} label={t(`employeeStatus.${emp.status}`)} />
                  </TableCell>
                  <TableCell style={{ textAlign: "end" }}>
                    <Button
                      variant="secondary"
                      size="compact"
                      onPress={() => onSelectEmployee(emp.id)}
                    >
                      {t("directory.viewProfile")}
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>

        {totalCount > pageSize && (
          <div style={{ padding: "0.75rem 1rem", borderTop: "1px solid var(--color-border-default)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("directory.totalRecords", { count: totalCount })}
            </span>
            <Pagination
              page={page}
              totalPages={totalPages}
              onPageChange={setPage}
            />
          </div>
        )}
      </div>

      {/* Creation Modal Dialog */}
      <NewEmployeeModal
        isOpen={isNewOpen}
        onClose={closeNew}
        legalEntityId={legalEntityId}
        onCreated={(id) => onSelectEmployee(id)}
      />
    </div>
  );
}
