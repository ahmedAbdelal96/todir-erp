import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { AccountStatusBadge } from "../../components/ledger-status-badges.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { AccountFormModal } from "./account-form-modal.tsx";
import { AccountWorkspaceModal } from "./account-workspace-modal.tsx";
import { useArchiveAccount } from "../../hooks/use-ledger-queries.ts";
import type { AccountDto, AccountType } from "../../types.ts";

export interface ChartOfAccountsViewProps {
  readonly legalEntityId: string;
  readonly accounts: readonly AccountDto[];
  readonly isLoading: boolean;
  readonly canManageAccounts: boolean;
  readonly onOpenGeneralLedger?: (accountCode: string) => void;
}

interface TreeNode {
  readonly account: AccountDto;
  readonly children: TreeNode[];
  readonly level: number;
}

export function ChartOfAccountsView({
  legalEntityId,
  accounts,
  isLoading,
  canManageAccounts,
  onOpenGeneralLedger
}: ChartOfAccountsViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const archiveAccountMutation = useArchiveAccount();

  const [searchTerm, setSearchTerm] = useState("");
  const [selectedType, setSelectedType] = useState<string>("ALL");
  const [viewMode, setViewMode] = useState<"tree" | "flat">("tree");

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedAccountId, setSelectedAccountId] = useState<string | null>(null);

  // Filter accounts
  const filteredAccounts = useMemo(() => {
    return accounts.filter((acc) => {
      const matchesSearch =
        acc.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        acc.nameAr.toLowerCase().includes(searchTerm.toLowerCase()) ||
        acc.nameEn.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesType = selectedType === "ALL" || acc.type === selectedType;
      return matchesSearch && matchesType;
    });
  }, [accounts, searchTerm, selectedType]);

  // Build tree representation
  const treeNodes = useMemo(() => {
    if (viewMode === "flat" || searchTerm.trim()) {
      return filteredAccounts.map((account) => ({
        account,
        children: [],
        level: 0
      }));
    }

    const byParent = new Map<string | null, AccountDto[]>();
    accounts.forEach((acc) => {
      const pId = acc.parentAccountId || null;
      const list = byParent.get(pId) || [];
      list.push(acc);
      byParent.set(pId, list);
    });

    const build = (parentId: string | null, level: number): TreeNode[] => {
      const list = byParent.get(parentId) || [];
      return list.map((account) => ({
        account,
        children: build(account.id, level + 1),
        level
      }));
    };

    const roots = build(null, 0);

    // Flatten tree with level indicator for table presentation
    const flattened: TreeNode[] = [];
    const traverse = (nodes: TreeNode[]) => {
      nodes.forEach((n) => {
        if (selectedType === "ALL" || n.account.type === selectedType) {
          flattened.push(n);
        }
        traverse(n.children);
      });
    };
    traverse(roots);
    return flattened;
  }, [accounts, filteredAccounts, viewMode, searchTerm, selectedType]);

  const handleArchive = async (accountId: string) => {
    if (window.confirm(t("account.confirmArchive"))) {
      await archiveAccountMutation.mutateAsync({ legalEntityId, accountId });
    }
  };

  const columns: readonly AccountingTableColumn<TreeNode>[] = [
    {
      key: "code",
      header: t("account.code"),
      width: "160px",
      minWidth: "140px",
      render: ({ account, level }) => (
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            paddingInlineStart: `${level * 1.5}rem`,
            fontWeight: level === 0 ? 700 : 500,
            fontFamily: "var(--font-mono, monospace)"
          }}
        >
          {level > 0 && <span style={{ opacity: 0.5, marginInlineEnd: "0.5rem" }}>↳</span>}
          <bdi>{account.code}</bdi>
        </span>
      )
    },
    {
      key: "name",
      header: t("account.name"),
      width: "320px",
      minWidth: "260px",
      render: ({ account, level }) => (
        <div style={{ minWidth: "240px", wordBreak: "break-word" }}>
          <span style={{ fontWeight: level === 0 ? 700 : 500 }}>{account.nameAr}</span>
          <span style={{ display: "block", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {account.nameEn}
          </span>
        </div>
      )
    },
    {
      key: "type",
      header: t("account.type"),
      width: "120px",
      minWidth: "110px",
      render: ({ account }) => <span>{t(`accountTypes.${account.type}`)}</span>
    },
    {
      key: "subtype",
      header: t("account.subtype"),
      width: "160px",
      minWidth: "140px",
      render: ({ account }) => <span>{t(`accountSubtypes.${account.subtype}` as any, { defaultValue: account.subtype })}</span>
    },
    {
      key: "normalBalance",
      header: t("account.normalBalance"),
      width: "110px",
      minWidth: "100px",
      render: ({ account }) => <span>{t(`normalBalances.${account.normalBalance}`)}</span>
    },
    {
      key: "directPosting",
      header: t("account.directPosting"),
      width: "110px",
      minWidth: "100px",
      align: "center",
      render: ({ account }) => (
        <span>{account.allowDirectPosting ? t("common.yes") : t("common.no")}</span>
      )
    },
    {
      key: "status",
      header: t("account.status"),
      width: "110px",
      minWidth: "100px",
      render: ({ account }) => <AccountStatusBadge status={account.status} />
    },
    {
      key: "actions",
      header: t("common.actions"),
      width: "180px",
      minWidth: "160px",
      align: "end",
      render: ({ account }) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
          <Button
            size="compact"
            variant="secondary"
            onPress={() => setSelectedAccountId(account.id)}
          >
            {t("account.details")}
          </Button>
          {canManageAccounts && account.status === "Active" && (
            <Button
              size="compact"
              variant="secondary"
              onPress={() => handleArchive(account.id)}
            >
              {t("account.archive")}
            </Button>
          )}
        </div>
      )
    }
  ];

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      {/* Header and Toolbar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          marginBottom: "1.25rem"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
            {t("account.chartTitle")}
          </h2>

          <div
            style={{
              display: "flex",
              borderRadius: "var(--radius-md, 0.5rem)",
              overflow: "hidden",
              border: "1px solid var(--color-border-default)"
            }}
          >
            <button
              type="button"
              onClick={() => setViewMode("tree")}
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                backgroundColor: viewMode === "tree" ? "var(--color-brand-primary)" : "var(--color-surface-card)",
                color: viewMode === "tree" ? "white" : "var(--color-content-primary)"
              }}
            >
              {t("account.treeView")}
            </button>
            <button
              type="button"
              onClick={() => setViewMode("flat")}
              style={{
                padding: "0.375rem 0.75rem",
                fontSize: "0.8125rem",
                fontWeight: 600,
                border: "none",
                cursor: "pointer",
                backgroundColor: viewMode === "flat" ? "var(--color-brand-primary)" : "var(--color-surface-card)",
                color: viewMode === "flat" ? "white" : "var(--color-content-primary)"
              }}
            >
              {t("account.flatView")}
            </button>
          </div>
        </div>

        {canManageAccounts && (
          <Button variant="primary" onPress={() => setIsCreateModalOpen(true)}>
            + {t("account.createNew")}
          </Button>
        )}
      </div>

      {/* Filters Bar */}
      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          flexWrap: "wrap",
          marginBottom: "1.25rem"
        }}
      >
        <div style={{ flex: "1 1 240px", maxWidth: "360px" }}>
          <TextField
            value={searchTerm}
            onChange={setSearchTerm}
            placeholder={t("account.searchPlaceholder")}
          />
        </div>

        <div>
          <select
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)}
            style={{
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)",
              fontSize: "0.875rem"
            }}
          >
            <option value="ALL">{t("account.allTypes")}</option>
            <option value="Asset">{t("accountTypes.Asset")}</option>
            <option value="Liability">{t("accountTypes.Liability")}</option>
            <option value="Equity">{t("accountTypes.Equity")}</option>
            <option value="Revenue">{t("accountTypes.Revenue")}</option>
            <option value="Expense">{t("accountTypes.Expense")}</option>
          </select>
        </div>
      </div>

      {/* Accounts Data Table */}
      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <AccountingDataTable
          minWidth="1250px"
          data={treeNodes}
          columns={columns}
          rowKey={(node) => node.account.id}
          emptyMessage={t("account.noAccountsFound")}
        />
      )}

      {/* Create Account Modal */}
      <AccountFormModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        legalEntityId={legalEntityId}
        accounts={accounts}
      />

      {/* Account Workspace Modal */}
      <AccountWorkspaceModal
        isOpen={Boolean(selectedAccountId)}
        onClose={() => setSelectedAccountId(null)}
        legalEntityId={legalEntityId}
        accountId={selectedAccountId}
        onOpenGeneralLedger={onOpenGeneralLedger}
      />
    </Surface>
  );
}
