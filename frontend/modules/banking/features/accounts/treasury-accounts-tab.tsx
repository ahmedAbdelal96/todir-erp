import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useTreasuryAccounts,
  useActivateTreasuryAccount,
  useSuspendTreasuryAccount,
  useCloseTreasuryAccount,
  useReopenTreasuryAccount
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { CreateTreasuryAccountModal } from "./create-treasury-account-modal.tsx";
import { TreasuryAccountDetailDrawer } from "./treasury-account-detail-drawer.tsx";
import type { TreasuryAccountDto, TreasuryAccountType, TreasuryAccountStatus } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
}

export function TreasuryAccountsTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState<TreasuryAccountType | "">("");
  const [statusFilter, setStatusFilter] = useState<TreasuryAccountStatus | "">("");
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedAccount, setSelectedAccount] = useState<TreasuryAccountDto | null>(null);

  const { data, isLoading, error, refetch } = useTreasuryAccounts(legalEntityId, {
    ...(search.trim() ? { search: search.trim() } : {}),
    ...(typeFilter ? { type: typeFilter } : {}),
    ...(statusFilter ? { status: statusFilter } : {}),
    page,
    pageSize
  });

  const activateMutation = useActivateTreasuryAccount(legalEntityId);
  const suspendMutation = useSuspendTreasuryAccount(legalEntityId);
  const closeMutation = useCloseTreasuryAccount(legalEntityId);
  const reopenMutation = useReopenTreasuryAccount(legalEntityId);

  const handleActivate = async (account: TreasuryAccountDto) => {
    try {
      await activateMutation.mutateAsync({ accountId: account.id, version: account.version });
    } catch {
      // ignore
    }
  };

  const handleSuspend = async (account: TreasuryAccountDto) => {
    try {
      await suspendMutation.mutateAsync({ accountId: account.id, version: account.version });
    } catch {
      // ignore
    }
  };

  const handleClose = async (account: TreasuryAccountDto) => {
    try {
      await closeMutation.mutateAsync({ accountId: account.id, version: account.version });
    } catch {
      // ignore
    }
  };

  const handleReopen = async (account: TreasuryAccountDto) => {
    try {
      await reopenMutation.mutateAsync({ accountId: account.id, version: account.version });
    } catch {
      // ignore
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("accounts.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("accounts.subtitle")}</p>
        </div>
        <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
          {t("accounts.newAccountButton")}
        </Button>
      </div>

      {/* Filter Surface */}
      <Surface style={{ padding: "1rem" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", alignItems: "flex-end" }}>
          <div>
            <TextField
              label={t("accounts.searchPlaceholder")}
              value={search}
              onChange={(val) => {
                setSearch(val);
                setPage(1);
              }}
            />
          </div>
          <div>
            <Select
              label={t("accounts.type")}
              selectedKey={typeFilter}
              onSelectionChange={(key) => {
                setTypeFilter((key as TreasuryAccountType | "") || "");
                setPage(1);
              }}
              options={[
                { id: "", label: t("accounts.allTypes") },
                { id: "Bank", label: t("accountTypes.Bank") },
                { id: "Cash", label: t("accountTypes.Cash") },
                { id: "PettyCash", label: t("accountTypes.PettyCash") },
                { id: "DigitalWallet", label: t("accountTypes.DigitalWallet") },
                { id: "ProviderSettlement", label: t("accountTypes.ProviderSettlement") }
              ]}
            />
          </div>
          <div>
            <Select
              label={t("accounts.status")}
              selectedKey={statusFilter}
              onSelectionChange={(key) => {
                setStatusFilter((key as TreasuryAccountStatus | "") || "");
                setPage(1);
              }}
              options={[
                { id: "", label: t("accounts.allStatuses") },
                { id: "Draft", label: t("accountStatuses.Draft") },
                { id: "Active", label: t("accountStatuses.Active") },
                { id: "Suspended", label: t("accountStatuses.Suspended") },
                { id: "Closing", label: t("accountStatuses.Closing") },
                { id: "Closed", label: t("accountStatuses.Closed") }
              ]}
            />
          </div>
        </div>
      </Surface>

      {/* Main Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : error ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>
            <p style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>{t("common.error")}</p>
            <Button variant="secondary" onClick={() => void refetch()} style={{ marginTop: "1rem" }}>
              {t("common.retry")}
            </Button>
          </div>
        ) : (data?.items ?? []).length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>
            <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("accounts.emptyTitle")}</h3>
            <p style={{ color: "var(--color-content-muted)", margin: "0 0 1.5rem" }}>{t("accounts.emptyDescription")}</p>
            <Button variant="primary" onClick={() => setIsCreateOpen(true)}>
              {t("accounts.emptyCta")}
            </Button>
          </div>
        ) : (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("accounts.code")}</TableHead>
                  <TableHead>{t("accounts.name")}</TableHead>
                  <TableHead>{t("accounts.type")}</TableHead>
                  <TableHead>{t("accounts.currency")}</TableHead>
                  <TableHead>{t("accounts.purpose")}</TableHead>
                  <TableHead>{t("accounts.liquidityClass")}</TableHead>
                  <TableHead>{t("accounts.maskedIdentifier")}</TableHead>
                  <TableHead>{t("accounts.status")}</TableHead>
                  <TableHead align="end">{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((acc) => (
                  <TableRow key={acc.id} onClick={() => setSelectedAccount(acc)} style={{ cursor: "pointer" }}>
                    <TableCell>
                      <span style={{ fontWeight: 600, color: "var(--color-brand-primary)" }}>
                        <BdiText>{acc.code}</BdiText>
                      </span>
                    </TableCell>
                    <TableCell style={{ fontWeight: 500 }}>{acc.name}</TableCell>
                    <TableCell>{t(`accountTypes.${acc.type}` as const)}</TableCell>
                    <TableCell>
                      <BdiText>{acc.currencyCode || t("currencyPolicies.MultiCurrency")}</BdiText>
                    </TableCell>
                    <TableCell>{acc.purpose}</TableCell>
                    <TableCell>{acc.liquidityClass}</TableCell>
                    <TableCell>
                      <BdiText>{acc.maskedIdentifier || "—"}</BdiText>
                    </TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={acc.status === "Active" ? "success" : acc.status === "Draft" ? "neutral" : "warning"}
                        label={t(`accountStatuses.${acc.status}` as const)}
                      />
                    </TableCell>
                    <TableCell align="end" onClick={(e) => e.stopPropagation()}>
                      <div style={{ display: "flex", gap: "0.375rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
                        <Button
                          size="compact"
                          variant="ghost"
                          onClick={() => setSelectedAccount(acc)}
                        >
                          {t("accounts.inspect")}
                        </Button>
                        {acc.status === "Draft" && (
                          <Button
                            size="compact"
                            variant="secondary"
                            onClick={() => handleActivate(acc)}
                            isDisabled={activateMutation.isPending}
                          >
                            {t("accounts.activate")}
                          </Button>
                        )}
                        {acc.status === "Active" && (
                          <Button
                            size="compact"
                            variant="ghost"
                            onClick={() => handleSuspend(acc)}
                            isDisabled={suspendMutation.isPending}
                          >
                            {t("accounts.suspend")}
                          </Button>
                        )}
                        {acc.status === "Suspended" && (
                          <Button
                            size="compact"
                            variant="secondary"
                            onClick={() => handleReopen(acc)}
                            isDisabled={reopenMutation.isPending}
                          >
                            {t("accounts.reopen")}
                          </Button>
                        )}
                        {acc.status !== "Closed" && acc.status !== "Closing" && (
                          <Button
                            size="compact"
                            variant="ghost"
                            onClick={() => handleClose(acc)}
                            isDisabled={closeMutation.isPending}
                          >
                            {t("accounts.close")}
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {data && data.totalCount > pageSize && (
              <div style={{ marginTop: "1rem", display: "flex", justifyContent: "center" }}>
                <Pagination
                  page={page}
                  totalPages={Math.ceil(data.totalCount / pageSize)}
                  onPageChange={setPage}
                />
              </div>
            )}
          </>
        )}
      </Surface>

      {/* Modals & Drawers */}
      <CreateTreasuryAccountModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      <TreasuryAccountDetailDrawer
        legalEntityId={legalEntityId}
        account={selectedAccount}
        isOpen={Boolean(selectedAccount)}
        onClose={() => setSelectedAccount(null)}
      />
    </div>
  );
}
