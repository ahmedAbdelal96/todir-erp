import { useState } from "react";
import { useTranslation } from "react-i18next";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { SearchField } from "../../../components/primitives/search-field.tsx";
import { FilterBar } from "../../../components/primitives/filter-bar.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../components/primitives/table.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { UsersIcon } from "../../../components/primitives/icons.tsx";
import { useUsersList, useLoginDomain, type UseIdentityContext } from "../hooks/identity-queries.ts";
import { CreateUserDialog } from "../features/create-user-dialog.tsx";
import { AdminPasswordResetDialog } from "../features/admin-password-reset-dialog.tsx";
import { DeactivateUserDialog } from "../features/deactivate-user-dialog.tsx";
import { ReactivateUserDialog } from "../features/reactivate-user-dialog.tsx";
import type { UserAccountSummary } from "../types.ts";

export interface UserListPageProps {
  readonly onSelectUser?: (user: UserAccountSummary) => void;
  readonly context?: UseIdentityContext | undefined;
}

export function UserListPage({ onSelectUser, context }: UserListPageProps): React.JSX.Element {
  const { t } = useTranslation("identity");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // Dialog States
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [resetTargetUser, setResetTargetUser] = useState<UserAccountSummary | null>(null);
  const [deactivateTargetUser, setDeactivateTargetUser] = useState<UserAccountSummary | null>(null);
  const [reactivateTargetUser, setReactivateTargetUser] = useState<UserAccountSummary | null>(null);

  const activeParam = statusFilter === "all" ? undefined : statusFilter === "active";

  const { data: usersPage, isLoading, error, refetch } = useUsersList(
    {
      search: searchTerm.trim() || undefined,
      active: activeParam,
      page,
      limit: pageSize
    },
    context
  );

  const { data: domainInfo } = useLoginDomain(context);
  const currentDomain = domainInfo?.loginDomain || "";

  const users = usersPage?.items ?? [];
  const totalCount = usersPage?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));


  return (
    <ListPage
      title={t("userList.title")}
      subtitle={t("userList.subtitle")}
      primaryAction={{
        label: t("userList.add"),
        onClick: () => setIsCreateOpen(true)
      }}
      pagination={
        totalCount > 0 ? (
          <Pagination
            page={page}
            totalPages={totalPages}
            totalItems={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
            onPageSizeChange={(newSize) => {
              setPageSize(newSize);
              setPage(1);
            }}
          />
        ) : undefined
      }
      filterBar={
        <FilterBar
          searchInput={
            <SearchField
              ariaLabel={t("userList.searchLabel")}
              placeholder={t("userList.searchPlaceholder")}
              value={searchTerm}
            onChange={(value) => { setSearchTerm(value); setPage(1); }}
            />
          }
          activeFilterCount={statusFilter !== "all" || searchTerm ? 1 : 0}
          onResetFilters={() => {
            setSearchTerm("");
            setStatusFilter("all");
            setPage(1);
          }}
        >
          <div style={{ minWidth: "160px" }}>
            <Select
              label={t("userList.accountStatus")}
              selectedKey={statusFilter}
              onSelectionChange={(key) => {
                setStatusFilter(String(key));
                setPage(1);
              }}
            >
              <SelectItem id="all" textValue={t("userList.allAccounts")}>{t("userList.allAccounts")}</SelectItem>
              <SelectItem id="active" textValue={t("userList.activeAccounts")}>{t("userList.activeAccounts")}</SelectItem>
              <SelectItem id="suspended" textValue={t("userList.suspendedAccounts")}>{t("userList.suspendedAccounts")}</SelectItem>
            </Select>
          </div>
        </FilterBar>
      }
    >
      {isLoading ? (
        <LoadingState message={t("userList.loading")} />
      ) : error ? (
        <ErrorState
          title={t("userList.loadError")}
          message={t("userList.loadErrorDescription")}
          onRetry={() => void refetch()}
        />
      ) : users.length === 0 ? (
        <EmptyState
          icon={<UsersIcon size={28} />}
          title={t("userList.emptyTitle")}
          description={
              searchTerm || statusFilter !== "all"
              ? t("userList.filteredEmpty")
              : t("userList.emptyDescription")
          }
          action={
            <Button variant="primary" onPress={() => setIsCreateOpen(true)}>
              {t("userList.addNew")}
            </Button>
          }
        />
      ) : (
        <div
          style={{
            backgroundColor: "var(--color-surface-card, #ffffff)",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid var(--color-border-default, #e2e8f0)",
            overflow: "hidden"
          }}
        >
          <Table aria-label={t("userList.tableLabel")}>
            <TableHeader>
              <TableRow>
                <TableHead style={{ width: "20%" }}>{t("userList.name")}</TableHead>
                <TableHead style={{ width: "25%" }}>{t("userList.loginEmail")}</TableHead>
                <TableHead style={{ width: "12%" }}>{t("userList.status")}</TableHead>
                <TableHead style={{ width: "13%" }}>{t("userList.temporaryPassword")}</TableHead>
                <TableHead style={{ width: "12%" }}>{t("userList.createdAt")}</TableHead>
                <TableHead style={{ width: "18%", textAlign: "center" }}>{t("userList.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((user) => {
                const isActive = user.status === "Active";
                return (
                  <TableRow key={user.id}>
                    <TableCell style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
                      {user.name}
                    </TableCell>

                    <TableCell>
                      <bdi dir="ltr" style={{ color: "var(--color-content-secondary, #334155)", fontFamily: "var(--font-mono, monospace)", fontSize: "0.875rem" }}>
                        {user.email}
                      </bdi>
                    </TableCell>

                    <TableCell>
                      <StatusBadge
                        intent={isActive ? "success" : "neutral"}
                        label={isActive ? t("userList.active") : t("userList.disabled")}
                        size="small"
                      />
                    </TableCell>

                    <TableCell>
                      {user.mustChangePassword ? (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-status-warning, #f59e0b)", fontWeight: 600 }}>
                          {t("userList.changePasswordRequired")}
                        </span>
                      ) : (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                          {t("userList.passwordSet")}
                        </span>
                      )}
                    </TableCell>

                    <TableCell style={{ color: "var(--color-content-muted, #64748b)", fontSize: "0.8125rem" }}>
                      {user.createdAt ? <LocalDateDisplay value={user.createdAt} /> : "—"}
                    </TableCell>

                    <TableCell>
                      <div style={{ display: "flex", gap: "0.5rem", justifyContent: "center" }}>
                        {onSelectUser && (
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => onSelectUser(user)}
                          >
                            {t("userList.details")}
                          </Button>
                        )}
                        <Button
                          variant="secondary"
                          size="compact"
                          onPress={() => setResetTargetUser(user)}
                        >
                          {t("userList.resetPassword")}
                        </Button>
                        {isActive ? (
                          <Button
                            variant="danger"
                            size="compact"
                            onPress={() => setDeactivateTargetUser(user)}
                          >
                            {t("userList.deactivate")}
                          </Button>
                        ) : (
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={() => setReactivateTargetUser(user)}
                          >
                            {t("userList.reactivate")}
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Feature Dialogs */}
      <CreateUserDialog
        isOpen={isCreateOpen}
        onOpenChange={setIsCreateOpen}
        loginDomain={currentDomain}
        context={context}
      />

      <AdminPasswordResetDialog
        isOpen={Boolean(resetTargetUser)}
        onOpenChange={(open) => !open && setResetTargetUser(null)}
        user={resetTargetUser}
        context={context}
      />

      <DeactivateUserDialog
        isOpen={Boolean(deactivateTargetUser)}
        onOpenChange={(open) => !open && setDeactivateTargetUser(null)}
        user={deactivateTargetUser}
        context={context}
      />

      <ReactivateUserDialog
        isOpen={Boolean(reactivateTargetUser)}
        onOpenChange={(open) => !open && setReactivateTargetUser(null)}
        user={reactivateTargetUser}
        context={context}
      />
    </ListPage>
  );
}
