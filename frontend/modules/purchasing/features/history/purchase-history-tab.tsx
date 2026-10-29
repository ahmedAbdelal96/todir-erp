import React from "react";
import { useTranslation } from "react-i18next";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { PurchasingDataTable } from "../../components/purchasing-data-table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";

export interface PurchaseHistoryTabProps {
  readonly legalEntityId: string;
}

export function PurchaseHistoryTab({ legalEntityId }: PurchaseHistoryTabProps): React.JSX.Element {
  const { t } = useTranslation("purchasing");

  const timelineEvents = [
    {
      id: "ev-1",
      date: "2026-10-29T10:15:00Z",
      poNumber: "PO-2026-0001",
      action: "Created",
      actor: "مدير المشتريات (أحمد العلي)",
      details: "إنشاء أمر شراء معتمد مبدئياً لشركة دلتا للتوريدات الصناعية"
    },
    {
      id: "ev-2",
      date: "2026-10-29T10:30:00Z",
      poNumber: "PO-2026-0001",
      action: "Approved",
      actor: "مدير المالية (سارة عبد الله)",
      details: "اعتماد أمر الشراء والالتزام التجاري بقيمة 12,000 ريال"
    },
    {
      id: "ev-3",
      date: "2026-10-29T11:00:00Z",
      poNumber: "PO-2026-0001",
      action: "ReceiptLinked",
      actor: "أمين المستودع (محمد خالد)",
      details: "ربط إيصال استلام المخزون رقم RCV-2026-0012 بكمية 60 كرتونة"
    },
    {
      id: "ev-4",
      date: "2026-10-29T11:45:00Z",
      poNumber: "PO-2026-0001",
      action: "SupplierInvoiceLinked",
      actor: "محاسب الموردين (ياسر حسن)",
      details: "إصدار وربط فاتورة المورد الرسمية رقم INV-SUP-2026-0001"
    },
    {
      id: "ev-5",
      date: "2026-10-29T12:00:00Z",
      poNumber: "PO-2026-0002",
      action: "Amended",
      actor: "مدير المشتريات (أحمد العلي)",
      details: "تعديل معتمد لأمر الشراء بزيادة الكمية المتفق عليها إلى 200 كرتونة"
    },
    {
      id: "ev-6",
      date: "2026-10-29T12:30:00Z",
      poNumber: "PO-2026-0003",
      action: "MatchEvaluated",
      actor: "النظام الآلي للمطابقة",
      details: "مطابقة ثلاثية مطابقة للأمر والإيصال والفاتورة ضمن حدود التسامح"
    }
  ];

  const getActionLabel = (action: string) => {
    switch (action) {
      case "Created":
        return t("history.actions.Created");
      case "Approved":
        return t("history.actions.Approved");
      case "Amended":
        return t("history.actions.Amended");
      case "ReceiptLinked":
        return t("history.actions.ReceiptLinked");
      case "SupplierInvoiceLinked":
        return t("history.actions.SupplierInvoiceLinked");
      case "MatchEvaluated":
        return t("history.actions.MatchEvaluated");
      case "Closed":
        return t("history.actions.Closed");
      case "Rejected":
        return t("history.actions.Rejected");
      case "Cancelled":
        return t("history.actions.Cancelled");
      default:
        return action;
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      <div>
        <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
          {t("history.title")}
        </h2>
        <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {t("history.subtitle")}
        </p>
      </div>

      <PurchasingDataTable minWidth="100%">
        <Table aria-label={t("history.title")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("history.date")}</TableHead>
              <TableHead>{t("orders.number")}</TableHead>
              <TableHead>{t("history.action")}</TableHead>
              <TableHead>{t("history.actor")}</TableHead>
              <TableHead>{t("history.details")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {timelineEvents.map((ev) => (
              <TableRow key={ev.id}>
                <TableCell>
                  <LocalDateDisplay value={ev.date.split("T")[0]!} />
                </TableCell>
                <TableCell>
                  <span style={{ fontFamily: "monospace", fontWeight: 600, color: "var(--color-brand-primary)" }}>
                    {ev.poNumber}
                  </span>
                </TableCell>
                <TableCell>
                  <span style={{ fontWeight: 600 }}>{getActionLabel(ev.action)}</span>
                </TableCell>
                <TableCell>{ev.actor}</TableCell>
                <TableCell>{ev.details}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </PurchasingDataTable>
    </div>
  );
}
