import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCrmCustomer360Query } from "../../hooks/use-crm-queries.ts";
import { Building2, TrendingUp, CheckCircle, HeartHandshake, DollarSign, Calendar } from "../../components/crm-icons.tsx";

interface Customer360ModalProps {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId: string;
  partyId: string;
  partyName?: string;
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
      <div className="space-y-6">
        {isLoading ? (
          <div className="py-12 text-center text-slate-500 text-sm">
            {t("crm.common.loading", "جاري تحميل بيانات العميل...")}
          </div>
        ) : isError || !customer360 ? (
          <div className="py-8 text-center text-rose-600 text-sm font-medium">
            {t("crm.customer360.loadError", "تعذر تحميل بيانات العميل 360°.")}
          </div>
        ) : (
          <div className="space-y-6">
            {/* Relationship Health Banner */}
            <div className="flex items-center justify-between p-4 bg-slate-50 rounded-lg border border-slate-200">
              <div className="flex items-center gap-2">
                <HeartHandshake className="h-5 w-5 text-primary-600" />
                <span className="text-sm font-semibold text-slate-700">
                  {t("crm.customer360.healthLabel", "صحة العلاقة التجارية:")}
                </span>
              </div>
              <div>
                <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${
                  customer360.relationshipHealth === "Good" || customer360.relationshipHealth === "جيد"
                    ? "bg-emerald-100 text-emerald-800 border border-emerald-300"
                    : customer360.relationshipHealth === "AtRisk" || customer360.relationshipHealth === "معرض للخطر"
                    ? "bg-rose-100 text-rose-800 border border-rose-300"
                    : "bg-amber-100 text-amber-800 border border-amber-300"
                }`}>
                  {customer360.relationshipHealth || t("crm.customer360.healthNormal", "مستقرة")}
                </span>
              </div>
            </div>

            {/* KPI Cards Grid */}
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              {/* Open Deals */}
              <div className="bg-blue-50/50 p-3.5 rounded-lg border border-blue-200 text-start">
                <div className="flex items-center gap-1.5 text-xs text-blue-700 font-medium mb-1">
                  <TrendingUp className="h-4 w-4" />
                  <span>{t("crm.customer360.openDeals", "فرص نشطة")}</span>
                </div>
                <div className="text-xl font-bold text-slate-900">
                  {customer360.openOpportunityCount}
                </div>
              </div>

              {/* Pipeline Value */}
              <div className="bg-indigo-50/50 p-3.5 rounded-lg border border-indigo-200 text-start">
                <div className="flex items-center gap-1.5 text-xs text-indigo-700 font-medium mb-1">
                  <DollarSign className="h-4 w-4" />
                  <span>{t("crm.customer360.pipelineVal", "قيمة قيد التفاوض")}</span>
                </div>
                <div className="text-base font-bold text-indigo-900 truncate">
                  {formatCurrency(customer360.openPipelineAmount)}
                </div>
              </div>

              {/* Status */}
              <div className="bg-emerald-50/50 p-3.5 rounded-lg border border-emerald-200 text-start">
                <div className="flex items-center gap-1.5 text-xs text-emerald-700 font-medium mb-1">
                  <CheckCircle className="h-4 w-4" />
                  <span>{t("crm.customer360.statusActive", "سجل عميل معتمد")}</span>
                </div>
                <div className="text-sm font-bold text-slate-900 pt-1">
                  {customer360.doNotContact
                    ? t("crm.customer360.doNotContact", "ممنوع التواصل")
                    : t("crm.customer360.canContact", "متاح للتواصل")}
                </div>
              </div>
            </div>

            {/* Last Activity / Interaction */}
            <div className="p-4 bg-white rounded-lg border border-slate-200 shadow-xs flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-slate-100 rounded text-slate-600">
                  <Calendar className="h-5 w-5" />
                </div>
                <div>
                  <div className="text-xs text-slate-500 font-medium">
                    {t("crm.customer360.lastInteraction", "آخر تفاعل مسجل")}
                  </div>
                  <div className="text-sm font-semibold text-slate-800">
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
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
};
