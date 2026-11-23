import React, { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { useQualityInspection } from "../hooks/use-quality-queries.ts";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  RecordResultModal,
  CompleteInspectionModal,
  ReinspectModal
} from "../components/inspection-modals.tsx";
import {
  CreateRestrictionModal,
  CreateDispositionModal
} from "../components/hold-disposition-modals.tsx";

export function InspectionWorkspacePage(): React.JSX.Element {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const { data: inspection, isLoading } = useQualityInspection(id);

  const [isRecordResultOpen, setIsRecordResultOpen] = useState(false);
  const [selectedCharId, setSelectedCharId] = useState<string>("");
  const [selectedCharName, setSelectedCharName] = useState<string>("");
  const [selectedDataType, setSelectedDataType] = useState<string>("Numeric");
  const [selectedUom, setSelectedUom] = useState<string | null>(null);

  const [isCompleteOpen, setIsCompleteOpen] = useState(false);
  const [isReinspectOpen, setIsReinspectOpen] = useState(false);
  const [isRestrictionOpen, setIsRestrictionOpen] = useState(false);
  const [isDispositionOpen, setIsDispositionOpen] = useState(false);

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  if (isLoading) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "#64748b" }}>
        {t("messages.loading", "جاري تحميل البيانات...")}
      </div>
    );
  }

  if (!inspection) {
    return (
      <Surface style={{ padding: "2.5rem", textAlign: "center", margin: "2rem auto", maxWidth: "600px" }}>
        <h3 style={{ color: "#dc2626", margin: "0 0 1rem" }}>{t("messages.notFound", "السجل المطلوب غير متوفر")}</h3>
        <Link to="/quality/inspections" style={{ color: "#0284c7", fontWeight: 600 }}>
          ← {t("tabs.inspections", "العودة لقائمة الفحوصات")}
        </Link>
      </Surface>
    );
  }

  const isCompleted = inspection.state === "Completed";

  return (
    <div data-testid="inspection-workspace-page" style={{ padding: "0 0 2rem 0" }}>
      {/* Breadcrumb Navigation */}
      <nav style={{ fontSize: "0.875rem", color: "#64748b", marginBottom: "0.75rem" }}>
        <Link to="/quality" style={{ color: "inherit", textDecoration: "none" }}>
          {t("navigation.quality", "إدارة الجودة")}
        </Link>
        {" / "}
        <Link to="/quality/inspections" style={{ color: "inherit", textDecoration: "none" }}>
          {t("tabs.inspections", "فحوصات الجودة")}
        </Link>
        {" / "}
        <span style={{ color: "#0f172a", fontWeight: 600 }}>{inspection.id.slice(0, 8)}</span>
      </nav>

      {/* Workbench Header */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "12px" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "0.5rem" }}>
              <h1 style={{ margin: 0, fontSize: "1.5rem", color: "#102a43", fontWeight: 700 }}>
                {t("navigation.inspections", "أمر فحص جودة")}: {inspection.lotReference || inspection.number}
              </h1>
              <QualityStatusBadge status={inspection.state} />
              {inspection.decisionState && <QualityStatusBadge status={inspection.decisionState} />}
            </div>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#64748b" }}>
              {t("fields.eventType", "نوع الحدث")}: <strong style={{ color: "#0f172a" }}>{inspection.eventType}</strong>
              {" · "}
              {t("fields.lotQuantity", "كمية الدفعة")}: <strong style={{ color: "#0f172a" }}>{inspection.lotQuantity}</strong>
              {" · "}
              {t("fields.sampleSize", "حجم العينة")}: <strong style={{ color: "#0f172a" }}>{inspection.sampleSize}</strong>
            </p>
          </div>

          {/* Workbench Actions */}
          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            <Button
              variant="primary"
              onPress={() => {
                setSelectedCharId("CHAR-DEFAULT");
                setSelectedCharName(t("fields.numericValue", "فحص الخاصية"));
                setSelectedDataType("Numeric");
                setIsRecordResultOpen(true);
              }}
            >
              + {t("actions.enterResults", "تسجيل نتائج القياس")}
            </Button>

            <Button
              variant="outline"
              isDisabled={isCompleted}
              onPress={() => setIsCompleteOpen(true)}
            >
              ✓ {t("actions.completeInspection", "إتمام واعتماد الفحص")}
            </Button>

            <Button
              variant="outline"
              onPress={() => setIsRestrictionOpen(true)}
            >
              {t("actions.createRestriction", "حظر واحتجاز الدفعة")}
            </Button>

            <Button
              variant="outline"
              onPress={() => setIsDispositionOpen(true)}
            >
              {t("actions.createDisposition", "إصدار قرار فني")}
            </Button>

            <Button
              variant="outline"
              onPress={() => setIsReinspectOpen(true)}
            >
              {t("actions.reinspect", "إعادة فحص")}
            </Button>
          </div>
        </div>
      </Surface>

      {/* Critical Invariant Banner */}
      <div
        data-testid="inspection-domain-invariant-notice"
        style={{
          display: "flex",
          alignItems: "center",
          gap: "10px",
          backgroundColor: "#fef3c7",
          border: "1px solid #fde68a",
          borderRadius: "8px",
          padding: "0.75rem 1rem",
          marginBottom: "1.25rem",
          color: "#92400e",
          fontSize: "0.875rem"
        }}
      >
        <div>
          <strong style={{ display: "block" }}>
            {t("messages.passedNote", "تنبيه تشغيلي صارم: نجاح الفحص لا يعني الإفراج التلقائي عن المخزون؛ يتطلب الإفراج إجراءً صريحاً.")}
          </strong>
        </div>
      </div>

      {/* Inspection Metrics / Progress Overview */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "1rem", marginBottom: "1.25rem" }}>
        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.sampledQuantity", "الكمية المفحوصة")}</span>
          <strong style={{ fontSize: "1.5rem", color: "#102a43" }}>{inspection.results?.length ?? 0} / {inspection.sampleSize}</strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.conformingCount", "المطابق السليم")}</span>
          <strong style={{ fontSize: "1.5rem", color: "#166534" }}>{inspection.results?.filter((r) => r.passed === true).length ?? 0}</strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.nonConformingCount", "غير المطابق")}</span>
          <strong style={{ fontSize: "1.5rem", color: (inspection.results?.filter((r) => r.passed === false).length ?? 0) > 0 ? "#dc2626" : "#475569" }}>
            {inspection.results?.filter((r) => r.passed === false).length ?? 0}
          </strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.lotQuantity", "كمية الدفعة")}</span>
          <strong style={{ fontSize: "1.5rem", color: "#102a43" }}>{inspection.lotQuantity}</strong>
        </Surface>
      </div>

      {/* Modals */}
      <RecordResultModal
        inspectionId={inspection.id}
        characteristicId={selectedCharId}
        characteristicName={selectedCharName}
        dataType={selectedDataType}
        uomCode={selectedUom}
        isOpen={isRecordResultOpen}
        onClose={() => setIsRecordResultOpen(false)}
      />

      <CompleteInspectionModal
        inspectionId={inspection.id}
        isOpen={isCompleteOpen}
        onClose={() => setIsCompleteOpen(false)}
      />

      <ReinspectModal
        inspectionId={inspection.id}
        isOpen={isReinspectOpen}
        onClose={() => setIsReinspectOpen(false)}
      />

      <CreateRestrictionModal
        legalEntityId={legalEntityId}
        defaultInspectionId={inspection.id}
        isOpen={isRestrictionOpen}
        onClose={() => setIsRestrictionOpen(false)}
      />

      <CreateDispositionModal
        legalEntityId={legalEntityId}
        defaultInspectionId={inspection.id}
        isOpen={isDispositionOpen}
        onClose={() => setIsDispositionOpen(false)}
      />
    </div>
  );
}
