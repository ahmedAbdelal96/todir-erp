import React from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { BookOpenIcon, CheckCircle2Icon } from "./fixed-assets-icons.tsx";

export interface FixedAssetsPageHelpProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function FixedAssetsPageHelp({
  isOpen,
  onClose
}: FixedAssetsPageHelpProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  return (
    <QuickPeekDrawer
      isOpen={isOpen}
      onClose={onClose}
      title={t("help.title", "دليل دورة حياة الأصول الثابتة")}
      width="560px"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }} data-testid="fixed-assets-help-drawer">
        <div
          style={{
            padding: "1rem",
            backgroundColor: "#f0f4f8",
            borderRadius: "0.5rem",
            border: "1px solid #cbd5e1"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem" }}>
            <BookOpenIcon size={20} color="#102a43" />
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("help.overviewHeading", "نظرة عامة على دورة الأصل")}
            </h3>
          </div>
          <p style={{ margin: 0, fontSize: "0.875rem", lineHeight: 1.6, color: "#334155" }}>
            {t("help.overviewText", "يوفر نظام الأصول الثابتة إمكانية إدارة شاملة للأصل من مرحلة الرأسمالية والمشاريع قيد التنفيذ (CIP) أو الشراء المباشر، مروراً بمسك الدفاتر المحاسبية والضريبية، وتتبع العهد الميدانية والباركود، وحتى الاستبعاد أو البيع والتخريد مع الترحيل الآلي إلى دفتر الأستاذ العام.")}
          </p>
        </div>

        <section>
          <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem", fontWeight: 600, color: "#0f172a" }}>
            {t("help.lifecycleStages", "مراحل دورة حياة الأصل")}
          </h4>
          <ul style={{ margin: 0, paddingInlineStart: "1.25rem", display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem", color: "#475569" }}>
            <li>
              <strong style={{ color: "#0f172a" }}>مسودة (Draft):</strong> الأصل قيد الإدخال أو المراجعة ولم يتم تفعيله محاسبياً بعد.
            </li>
            <li>
              <strong style={{ color: "#0f172a" }}>قيد التنفيذ (Under Construction / CIP):</strong> أصل رأسمالي قيد الإنشاء تتراكم عليه التكاليف حتى تاريخ الجاهزية للتشغيل.
            </li>
            <li>
              <strong style={{ color: "#0f172a" }}>نشط (Active):</strong> أصل في الخدمة يتم احتساب إهلاكه شهرياً وفق طريقة الإهلاك المحددة.
            </li>
            <li>
              <strong style={{ color: "#0f172a" }}>مستهلك بالكامل (Fully Depreciated):</strong> وصل صافي القيمة الدفترية إلى قيمة الخردة ولا زال في الخدمة.
            </li>
            <li>
              <strong style={{ color: "#0f172a" }}>مستبعد / تم التخريد (Disposed / Scrapped):</strong> تم بيع الأصل أو تخريده أو فقده مع تسجيل قيود الأرباح والخسائر الناتجة.
            </li>
          </ul>
        </section>

        <section>
          <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem", fontWeight: 600, color: "#0f172a" }}>
            {t("help.depreciationRules", "قواعد الإهلاك المحاسبي")}
          </h4>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem", color: "#475569" }}>
            <div style={{ display: "flex", alignItems: "flex-start", gap: "0.5rem" }}>
              <CheckCircle2Icon size={16} color="#16a34a" style={{ marginTop: "0.2rem", flexShrink: 0 }} />
              <span><strong>القسط الثابت (Straight Line):</strong> يوزع مجمع التكلفة القابلة للإهلاك بالتساوي على أشهر العمر الإنتاجي.</span>
            </div>
            <div style={{ display: "flex", alignItems: "flex-start", gap: "0.5rem" }}>
              <CheckCircle2Icon size={16} color="#16a34a" style={{ marginTop: "0.2rem", flexShrink: 0 }} />
              <span><strong>الرصيد المتناقص (Declining Balance):</strong> يحتسب الإهلاك كنسبة مئوية من صافي القيمة الدفترية المتبقية.</span>
            </div>
            <div style={{ display: "flex", alignItems: "flex-start", gap: "0.5rem" }}>
              <CheckCircle2Icon size={16} color="#16a34a" style={{ marginTop: "0.2rem", flexShrink: 0 }} />
              <span><strong>الإهلاك غير الخطي للأراضي:</strong> الأراضي غير قابلة للإهلاك ولا يتم إنشاء مسيرات إهلاك لها.</span>
            </div>
          </div>
        </section>

        <section>
          <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem", fontWeight: 600, color: "#0f172a" }}>
            {t("help.custodyAudits", "الجرد الميداني وتتبع العهد")}
          </h4>
          <p style={{ margin: 0, fontSize: "0.875rem", lineHeight: 1.6, color: "#475569" }}>
            {t("help.custodyText", "يمكن ربط كل أصل بموقع جغرافي (مبنى، طابق، غرفة) وموظف مسؤول (عهدة). تدعم المنظومة حملات الجرد الدوري ومسح الباركود لتسجيل الأصول المفقودة أو التي تم نقلها دون إذن مسبق.")}
          </p>
        </section>

        <div style={{ display: "flex", justifyContent: "flex-end", paddingTop: "1rem", borderTop: "1px solid #cbd5e1" }}>
          <Button variant="secondary" onClick={onClose}>
            {t("common.close", "إغلاق")}
          </Button>
        </div>
      </div>
    </QuickPeekDrawer>
  );
}
