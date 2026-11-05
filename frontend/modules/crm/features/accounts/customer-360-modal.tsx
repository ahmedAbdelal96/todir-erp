import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCrmCustomer360Query } from "../../hooks/use-crm-queries.ts";
import { HeartHandshake, TrendingUp, DollarSign, CheckCircle, Calendar } from "../../components/crm-icons.tsx";

interface Customer360ModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly partyName?: string;
}

export const Customer360Modal: React.FC<Customer360ModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
  partyId,
  partyName,
}) => {
  const { t, i18n } = useTranslation("crm");
  const { data: customer360, isLoading, isError } = useCrmCustomer360Query(legalEntityId, partyId);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(i18n.language, {
      style: "currency",
      currency: "EGP",
      maximumFractionDigits: 0,
    }).format(val || 0);
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => !open && onClose()}
      title={customer360?.displayName || partyName || t("crm.customer360.title", "ملف العميل 360°")}
      size="medium"
      footer={
        <Button variant="secondary" onPress={onClose}>
          {t("crm.common.close", "إغلاق")}
        </Button>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("crm.common.loading", "جاري تحميل بيانات العميل...")}
          </div>
        ) : isError || !customer360 ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-status-danger)", fontSize: "0.875rem", fontWeight: 600 }}>
            {t("crm.customer360.loadError", "تعذر تحميل بيانات العميل 360°.")}
          </div>
        ) : (
          <>
            {/* Relationship Health Banner */}
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                padding: "1rem 1.25rem",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                <HeartHandshake style={{ width: "1.25rem", height: "1.25rem", color: "var(--color-brand-primary, #102a43)" }} />
                <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
                  {t("crm.customer360.healthLabel", "صحة العلاقة التجارية:")}
                </span>
              </div>
              <span
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  padding: "0.25rem 0.75rem",
                  borderRadius: "9999px",
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  backgroundColor:
                    customer360.relationshipHealth === "Good" || customer360.relationshipHealth === "جيد"
                      ? "#ecfdf5"
                      : customer360.relationshipHealth === "AtRisk" || customer360.relationshipHealth === "معرض للخطر"
                      ? "#fef2f2"
                      : "#fffbeb",
                  color:
                    customer360.relationshipHealth === "Good" || customer360.relationshipHealth === "جيد"
                      ? "#065f46"
                      : customer360.relationshipHealth === "AtRisk" || customer360.relationshipHealth === "معرض للخطر"
                      ? "#991b1b"
                      : "#92400e",
                  border: `1px solid ${
                    customer360.relationshipHealth === "Good" || customer360.relationshipHealth === "جيد"
                      ? "#a7f3d0"
                      : customer360.relationshipHealth === "AtRisk" || customer360.relationshipHealth === "معرض للخطر"
                      ? "#fecaca"
                      : "#fde68a"
                  }`
                }}
              >
                {customer360.relationshipHealth || t("crm.customer360.healthNormal", "مستقرة")}
              </span>
            </div>

            {/* KPI Cards Grid */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: "0.75rem" }}>
              <div style={{ backgroundColor: "#eff6ff", padding: "0.875rem", borderRadius: "var(--radius-md)", border: "1px solid #bfdbfe" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.75rem", color: "#1e40af", fontWeight: 600, marginBottom: "0.25rem" }}>
                  <TrendingUp style={{ width: "1rem", height: "1rem" }} />
                  <span>{t("crm.customer360.openDeals", "فرص نشطة")}</span>
                </div>
                <div style={{ fontSize: "1.25rem", fontWeight: 800, color: "var(--color-content-primary)" }}>
                  {customer360.openOpportunityCount}
                </div>
              </div>

              <div style={{ backgroundColor: "#eef2ff", padding: "0.875rem", borderRadius: "var(--radius-md)", border: "1px solid #c7d2fe" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.75rem", color: "#3730a3", fontWeight: 600, marginBottom: "0.25rem" }}>
                  <DollarSign style={{ width: "1rem", height: "1rem" }} />
                  <span>{t("crm.customer360.pipelineVal", "قيمة قيد التفاوض")}</span>
                </div>
                <div style={{ fontSize: "1rem", fontWeight: 800, color: "#1e1b4b" }}>
                  <bdi>{formatCurrency(customer360.openPipelineAmount)}</bdi>
                </div>
              </div>

              <div style={{ backgroundColor: "#ecfdf5", padding: "0.875rem", borderRadius: "var(--radius-md)", border: "1px solid #a7f3d0" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.75rem", color: "#065f46", fontWeight: 600, marginBottom: "0.25rem" }}>
                  <CheckCircle style={{ width: "1rem", height: "1rem" }} />
                  <span>{t("crm.customer360.statusActive", "سجل عميل معتمد")}</span>
                </div>
                <div style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-primary)", paddingTop: "0.125rem" }}>
                  {customer360.doNotContact
                    ? t("crm.customer360.doNotContact", "ممنوع التواصل")
                    : t("crm.customer360.canContact", "متاح للتواصل")}
                </div>
              </div>
            </div>

            {/* Last Activity / Interaction */}
            <div
              style={{
                padding: "1rem 1.25rem",
                backgroundColor: "var(--color-surface-card)",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                display: "flex",
                alignItems: "center",
                gap: "0.75rem"
              }}
            >
              <div style={{ padding: "0.5rem", borderRadius: "var(--radius-sm)", backgroundColor: "var(--color-surface-subtle)", color: "var(--color-content-muted)" }}>
                <Calendar style={{ width: "1.25rem", height: "1.25rem" }} />
              </div>
              <div>
                <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontWeight: 500 }}>
                  {t("crm.customer360.lastInteraction", "آخر تفاعل مسجل")}
                </div>
                <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary)", marginTop: "0.125rem" }}>
                  {customer360.lastContactAt ? (
                    new Date(customer360.lastContactAt).toLocaleDateString(i18n.language, {
                      year: "numeric",
                      month: "long",
                      day: "numeric",
                    })
                  ) : (
                    t("crm.customer360.noInteraction", "لا يوجد تواصل سابق مسجل")
                  )}
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
};
