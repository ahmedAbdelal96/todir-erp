import { lazy, useState } from "react";
import type { ModuleRouteRegistration } from "@app/router/route-contract.ts";
import { Button } from "@ui/primitives/button.tsx";

const PaymentEntryDialog = lazy(() => import("./features/payment-entry-dialog.tsx").then(module => ({ default: module.PaymentEntryDialog })));

function PaymentsRoute(): React.JSX.Element {
  const [open, setOpen] = useState(false);
  return (
    <div style={{ padding: "1.5rem" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
        <div>
          <h1 style={{ margin: 0, fontSize: "1.5rem" }}>المدفوعات والمقبوضات</h1>
          <p style={{ margin: "0.25rem 0 0 0", color: "var(--color-content-muted)" }}>إدارة العمليات المالية والتحويلات المصرفية</p>
        </div>
      </div>
      <Button variant="primary" onPress={() => setOpen(true)}>+ تسجيل دفعة مالية جديدة</Button>
      <PaymentEntryDialog isOpen={open} onOpenChange={setOpen} onPaymentComplete={() => setOpen(false)} />
    </div>
  );
}

export const paymentsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "payments",
    moduleIds: ["payments"],
    path: "/payments",
    label: { ar: "المدفوعات", en: "Payments" },
    permission: "payments.transactions.read",
    icon: "payments"
  },
  routes: [{ path: "payments", element: <PaymentsRoute /> }]
};
