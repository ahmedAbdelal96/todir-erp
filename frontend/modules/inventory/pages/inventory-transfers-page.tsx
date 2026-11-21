import React from "react";
import { useTranslation } from "react-i18next";
import { Link, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useInventoryScope, useWarehouses, useInventoryPermissions } from "../hooks/use-inventory-queries.ts";
import { TransfersTab } from "../features/transfers/transfers-tab.tsx";

export function InventoryTransfersPage(): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId } = useInventoryScope({ legalEntityId: outletCtx?.legalEntityId });
  const permissions = useInventoryPermissions();
  const { data: warehouses = [] } = useWarehouses(legalEntityId);

  return (
    <div
      data-testid="inventory-transfers-page"
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
        <nav style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", marginBottom: "0.25rem" }}>
          <Link to="/inventory" style={{ color: "inherit", textDecoration: "none" }}>
            {t("title", "إدارة المخزون")}
          </Link>
          {" / "}
          <span style={{ color: "var(--color-content-primary)", fontWeight: 600 }}>
            {t("tabs.transfers", "أوامر التحويل بين المستودعات")}
          </span>
        </nav>
        <h1 style={{ margin: "0 0 0.25rem", fontSize: "1.5rem", color: "var(--color-brand-primary, #102a43)", fontWeight: 700 }}>
          {t("tabs.transfers", "أوامر التحويل بين المستودعات")}
        </h1>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("transfers.subtitle", "إنشاء ومتابعة أوامر التحويل المخزني بين الفروع والمستودعات والتأكيد على استلامها")}
        </p>
      </header>

      <main>
        <TransfersTab
          legalEntityId={legalEntityId}
          warehouses={warehouses}
          canCreate={permissions.canCreateTransfer}
          canShip={permissions.canShipTransfer}
          canReceive={permissions.canReceiveTransfer}
        />
      </main>
    </div>
  );
}
