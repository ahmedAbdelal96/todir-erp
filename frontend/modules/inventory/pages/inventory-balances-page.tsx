import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import {
  useInventoryScope,
  useWarehouses,
  useStockBalances
} from "../hooks/use-inventory-queries.ts";
import { StockTab } from "../features/stock/stock-tab.tsx";
import { ReceiveStockModal } from "../features/operations/receive-stock-modal.tsx";
import { IssueStockModal } from "../features/operations/issue-stock-modal.tsx";
import { TransferModal } from "../features/transfers/transfer-modal.tsx";

export function InventoryBalancesPage(): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useInventoryScope({ legalEntityId: outletCtx?.legalEntityId });

  const [isReceiveOpen, setIsReceiveOpen] = useState(false);
  const [isIssueOpen, setIsIssueOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);

  const { data: warehouses = [] } = useWarehouses(legalEntityId);
  const { data: balances = [] } = useStockBalances(legalEntityId);

  return (
    <div
      data-testid="inventory-balances-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box",
        minWidth: 0
      }}
    >
      <header
        style={{
          marginBottom: "1.25rem",
          paddingBottom: "1rem",
          borderBottom: "1px solid var(--color-border-default)"
        }}
      >
        <nav  style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
          <Link to="/inventory" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "إدارة المخزون")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.stock", "أرصدة ومواقع المخزون")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.stock", "أرصدة ومواقع المخزون")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("stock.subtitle", "متابعة الكميات المتاحة والمحجوزة ومواقع التخزين والأرصدة حسب المستودعات")}
        </p>
      </header>

      <main>
        <StockTab
          legalEntityId={legalEntityId}
          balances={balances}
          warehouses={warehouses}
          onOpenReceive={() => setIsReceiveOpen(true)}
          onOpenIssue={() => setIsIssueOpen(true)}
          onOpenTransfer={() => setIsTransferOpen(true)}
        />
      </main>

      {/* Operational Modals */}
      {isReceiveOpen && (
        <ReceiveStockModal
          isOpen={isReceiveOpen}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          onClose={() => setIsReceiveOpen(false)}
        />
      )}
      {isIssueOpen && (
        <IssueStockModal
          isOpen={isIssueOpen}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          onClose={() => setIsIssueOpen(false)}
        />
      )}
      {isTransferOpen && (
        <TransferModal
          isOpen={isTransferOpen}
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          onClose={() => setIsTransferOpen(false)}
        />
      )}
    </div>
  );
}
