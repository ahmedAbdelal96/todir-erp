import { lazy, useState } from "react";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";
import { Button } from "@ui/primitives/button.tsx";
import { useTranslation } from "react-i18next";

const PaymentEntryDialog = lazy(() => import("./features/payment-entry-dialog.tsx").then(module => ({ default: module.PaymentEntryDialog })));

function PaymentsRoute(): React.JSX.Element {
  const { t } = useTranslation("payments");
  const [open, setOpen] = useState(false);
  return (
    <div style={{ padding: "1.5rem" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
        <div>
          <h1 style={{ margin: 0, fontSize: "1.5rem" }}>{t("title")}</h1>
          <p style={{ margin: "0.25rem 0 0 0", color: "var(--color-content-muted)" }}>{t("subtitle")}</p>
        </div>
      </div>
      <Button variant="primary" onPress={() => setOpen(true)}>+ {t("newPayment")}</Button>
      <PaymentEntryDialog isOpen={open} onOpenChange={setOpen} onPaymentComplete={() => setOpen(false)} />
    </div>
  );
}

export const paymentsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "payments",
    moduleIds: ["payments"],
    path: "/payments",
    navigationKey: "payments",
    permission: "payments.transactions.read",
    icon: "payments"
  },
  routes: [{ path: "payments", element: <PaymentsRoute /> }]
};
