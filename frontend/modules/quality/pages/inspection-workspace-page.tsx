import React, { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { useQualityInspection, useQualityCharacteristics } from "../hooks/use-quality-queries.ts";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityAccess } from "../hooks/use-quality-access.ts";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";
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
  const { data: characteristicsData, isLoading: isCharsLoading } = useQualityCharacteristics(
    inspection?.legalEntityId ?? legalEntityId
  );
  const {
    isReadOnly,
    canExecuteInspection,
    canReviewInspection,
    canCreateInspection,
    canManageHolds,
    canManageDispositions
  } = useQualityAccess();

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
  const planSnapshotChars = inspection.planCharacteristics && inspection.planCharacteristics.length > 0 ? inspection.planCharacteristics : null;
  const characteristics = planSnapshotChars
    ? planSnapshotChars.map((p) => ({
        id: p.characteristicId,
        code: p.code,
        name: p.name,
        dataType: p.dataType,
        baseUomCode: p.baseUomCode,
        minimum: p.minimum,
        maximum: p.maximum,
        target: p.target,
        required: p.required,
        evidenceRequired: p.evidenceRequired,
        isFromFrozenSnapshot: true
      }))
    : (characteristicsData?.items ?? []).map((c) => ({
        id: c.id,
        code: c.code,
        name: c.name,
        dataType: c.dataType,
        baseUomCode: c.baseUomCode,
        minimum: null as number | null,
        maximum: null as number | null,
        target: null as string | null,
        required: c.required,
        evidenceRequired: false,
        isFromFrozenSnapshot: false
      }));
  const results = inspection.results ?? [];

  const openRecordModalForChar = (c: { id: string; name: string; dataType?: string; baseUomCode?: string | null }) => {
    setSelectedCharId(c.id);
    setSelectedCharName(c.name);
    setSelectedDataType(c.dataType || "Numeric");
    setSelectedUom(c.baseUomCode || null);
    setIsRecordResultOpen(true);
  };

  const handleOpenGeneralRecordModal = () => {
    // If there is an unrecorded characteristic, pre-select it
    const unrecorded = characteristics.find(
      (c) => !results.some((r) => r.characteristicId === c.id)
    );
    if (unrecorded) {
      openRecordModalForChar(unrecorded);
    } else if (characteristics.length > 0 && characteristics[0]) {
      openRecordModalForChar(characteristics[0]);
    } else {
      setSelectedCharId("");
      setSelectedCharName("");
      setSelectedDataType("Numeric");
      setSelectedUom(null);
      setIsRecordResultOpen(true);
    }
  };

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
        <span style={{ color: "#0f172a", fontWeight: 600 }}>
          <BdiText>{inspection.number || inspection.id.slice(0, 8)}</BdiText>
        </span>
      </nav>

      {/* Workbench Header */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "12px" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "0.5rem" }}>
              <h1 style={{ margin: 0, fontSize: "1.5rem", color: "#102a43", fontWeight: 700 }}>
                {t("navigation.inspections", "أمر فحص جودة")}: <BdiText>{inspection.lotReference || inspection.number}</BdiText>
              </h1>
              <QualityStatusBadge status={inspection.state} />
              {inspection.decisionState && <QualityStatusBadge status={inspection.decisionState} />}
            </div>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#64748b" }}>
              {t("fields.eventType", "نوع الحدث")}: <strong style={{ color: "#0f172a" }}>{inspection.eventType}</strong>
              {" · "}
              {t("fields.lotQuantity", "كمية الدفعة")}: <strong style={{ color: "#0f172a" }}><BdiText>{String(inspection.lotQuantity)}</BdiText></strong>
              {" · "}
              {t("fields.sampleSize", "حجم العينة")}: <strong style={{ color: "#0f172a" }}><BdiText>{String(inspection.sampleSize)}</BdiText></strong>
            </p>
          </div>

          {/* Workbench Actions */}
          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            <Button
              variant="primary"
              isDisabled={isCompleted || !canExecuteInspection || isReadOnly}
              onPress={handleOpenGeneralRecordModal}
            >
              + {t("actions.enterResults", "تسجيل نتائج القياس")}
            </Button>

            <Button
              variant="outline"
              isDisabled={isCompleted || !canReviewInspection || isReadOnly}
              onPress={() => setIsCompleteOpen(true)}
            >
              ✓ {t("actions.completeInspection", "إتمام واعتماد الفحص")}
            </Button>

            <Button
              variant="outline"
              isDisabled={!canManageHolds || isReadOnly}
              onPress={() => setIsRestrictionOpen(true)}
            >
              {t("actions.createRestriction", "حظر واحتجاز الدفعة")}
            </Button>

            <Button
              variant="outline"
              isDisabled={!canManageDispositions || isReadOnly}
              onPress={() => setIsDispositionOpen(true)}
            >
              {t("actions.createDisposition", "إصدار قرار فني")}
            </Button>

            <Button
              variant="outline"
              isDisabled={!isCompleted || !canCreateInspection || isReadOnly}
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
          <strong style={{ fontSize: "1.5rem", color: "#102a43" }}><BdiText>{`${results.length} / ${inspection.sampleSize}`}</BdiText></strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.conformingCount", "المطابق السليم")}</span>
          <strong style={{ fontSize: "1.5rem", color: "#166534" }}><BdiText>{String(results.filter((r) => r.passed === true).length)}</BdiText></strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.nonConformingCount", "غير المطابق")}</span>
          <strong style={{ fontSize: "1.5rem", color: (results.filter((r) => r.passed === false).length) > 0 ? "#dc2626" : "#475569" }}>
            <BdiText>{String(results.filter((r) => r.passed === false).length)}</BdiText>
          </strong>
        </Surface>

        <Surface style={{ padding: "1rem", border: "1px solid #e2e8f0", borderRadius: "8px", textAlign: "center" }}>
          <span style={{ fontSize: "0.75rem", color: "#64748b", display: "block" }}>{t("fields.lotQuantity", "كمية الدفعة")}</span>
          <strong style={{ fontSize: "1.5rem", color: "#102a43" }}><BdiText>{String(inspection.lotQuantity)}</BdiText></strong>
        </Surface>
      </div>

      {/* Main Characteristics & Measurements Table */}
      <Surface style={{ padding: "1.25rem", marginBottom: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <h2 style={{ margin: 0, fontSize: "1.125rem", color: "#102a43", fontWeight: 700 }}>
            {t("tabs.characteristics", "خصائص الفحص والمواصفات القياسية")}
          </h2>
          <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
            {planSnapshotChars
              ? t("messages.frozenSnapshotActive", "معايير الخطة المجمدة المعتمدة")
              : (isCharsLoading ? t("messages.loading", "جاري التحميل...") : t("messages.registeredCharacteristicsCount", { count: characteristics.length, defaultValue: `${characteristics.length} خاصية مسجلة` }))}
          </span>
        </div>

        {characteristics.length === 0 ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "#64748b", backgroundColor: "#f8fafc", borderRadius: "6px" }}>
            <p style={{ margin: "0 0 0.5rem" }}>{t("messages.noData", "لا توجد خصائص فحص مسجلة لهذا الكيان.")}</p>
            <Link to="/quality/plans" style={{ color: "#0284c7", fontWeight: 600 }}>
              ← {t("actions.createCharacteristic", "إضافة معايير فحص جديدة")}
            </Link>
          </div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table data-testid="inspection-characteristics-table" style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #cbd5e1", textAlign: "start", color: "#475569" }}>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.code", "الكود")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.name", "خاصية الفحص")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.dataType", "نوع البيانات")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.required", "الإلزامية")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.numericValue", "القياس المسجل")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.evaluation", "المطابقة")}</th>
                  <th style={{ padding: "0.75rem 0.5rem", textAlign: "end" }}>{t("fields.status", "الإجراء")}</th>
                </tr>
              </thead>
              <tbody>
                {characteristics.map((char) => {
                  const result = results.find((r) => r.characteristicId === char.id);
                  return (
                    <tr key={char.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "0.75rem 0.5rem", fontWeight: 600, color: "#1e293b" }}>
                        <BdiText>{char.code}</BdiText>
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem", color: "#0f172a" }}>
                        <div style={{ fontWeight: 600 }}>{char.name}</div>
                        {char.target && (
                          <div style={{ fontSize: "0.75rem", color: "#0284c7", marginTop: "2px" }}>
                            {t("fields.target", "المستهدف")}: <BdiText>{char.target}</BdiText>
                          </div>
                        )}
                        {(char.minimum !== null && char.minimum !== undefined || char.maximum !== null && char.maximum !== undefined) && (
                          <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "1px" }}>
                            {t("fields.tolerance", "الحدود")}: <BdiText>{t("fields.toleranceRange", { min: char.minimum ?? "—", max: char.maximum ?? "—", defaultValue: `${char.minimum ?? "—"} - ${char.maximum ?? "—"}` })}</BdiText>
                          </div>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem", color: "#64748b" }}>
                        {char.dataType}
                        {char.baseUomCode && <span style={{ marginInlineStart: "4px" }}>(<BdiText>{char.baseUomCode}</BdiText>)</span>}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem" }}>
                        <div style={{ display: "flex", gap: "4px", flexWrap: "wrap" }}>
                          <span
                            style={{
                              padding: "2px 8px",
                              borderRadius: "4px",
                              fontSize: "0.75rem",
                              fontWeight: 600,
                              backgroundColor: char.required ? "#fef2f2" : "#f1f5f9",
                              color: char.required ? "#b91c1c" : "#475569"
                            }}
                          >
                            {char.required ? t("fields.requiredYes", "إلزامي") : t("fields.requiredNo", "اختياري")}
                          </span>
                          {char.evidenceRequired && (
                            <span
                              style={{
                                padding: "2px 8px",
                                borderRadius: "4px",
                                fontSize: "0.75rem",
                                fontWeight: 600,
                                backgroundColor: "#eff6ff",
                                color: "#1d4ed8"
                              }}
                            >
                              {t("fields.evidenceRequired", "مستند إلزامي")}
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem" }}>
                        {result ? (
                          <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                            <span style={{ fontWeight: 600, color: "#0f172a" }}>
                              {result.numericValue !== null && result.numericValue !== undefined && (
                                <BdiText>{`${result.numericValue} ${result.uomCode || char.baseUomCode || ""}`}</BdiText>
                              )}
                              {result.textValue && <BdiText>{result.textValue}</BdiText>}
                              {result.booleanValue !== null && result.booleanValue !== undefined && (
                                <QualityStatusBadge status={result.booleanValue ? "Passed" : "Failed"} />
                              )}
                            </span>
                            {result.evidenceReference && (
                              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>
                                {t("fields.document", "مستند")}: <BdiText>{result.evidenceReference}</BdiText>
                              </span>
                            )}
                          </div>
                        ) : (
                          <span style={{ color: "#d97706", fontSize: "0.8125rem", fontWeight: 500 }}>
                            {t("messages.awaitingMeasurement", "بانتظار القياس")}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem" }}>
                        {result ? (
                          <QualityStatusBadge status={result.passed ? "Passed" : "Failed"} />
                        ) : (
                          <span style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem", textAlign: "end" }}>
                        <Button
                          variant={result ? "outline" : "primary"}
                          size="small"
                          isDisabled={isCompleted || !canExecuteInspection || isReadOnly}
                          onPress={() => openRecordModalForChar(char)}
                        >
                          {result ? t("actions.reMeasure", "تعديل / إعادة قياس") : t("actions.measure", "+ تسجيل القياس")}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Surface>

      {/* Recorded Results Log */}
      {results.length > 0 && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          <h2 style={{ margin: "0 0 1rem", fontSize: "1.125rem", color: "#102a43", fontWeight: 700 }}>
            {t("actions.enterResults", "سجل القياسات والنتائج المسجلة")} ({results.length})
          </h2>
          <div style={{ overflowX: "auto" }}>
            <table data-testid="inspection-results-log-table" style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #cbd5e1", textAlign: "start", color: "#475569" }}>
                  <th style={{ padding: "0.75rem 0.5rem" }}>#</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.name", "خاصية الفحص")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.numericValue", "القيمة")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.evaluation", "التقييم")}</th>
                  <th style={{ padding: "0.75rem 0.5rem" }}>{t("fields.evidence", "مرجع الإثبات")}</th>
                </tr>
              </thead>
              <tbody>
                {results.map((res, idx) => {
                  const matchedChar = characteristics.find((c) => c.id === res.characteristicId);
                  return (
                    <tr key={res.id || idx} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "0.75rem 0.5rem", color: "#64748b" }}>{idx + 1}</td>
                      <td style={{ padding: "0.75rem 0.5rem", fontWeight: 600, color: "#1e293b" }}>
                        {matchedChar ? `${matchedChar.code} - ${matchedChar.name}` : <BdiText>{res.characteristicId.slice(0, 8)}</BdiText>}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem", color: "#0f172a" }}>
                        {res.numericValue !== null && res.numericValue !== undefined && (
                          <BdiText>{`${res.numericValue} ${res.uomCode || matchedChar?.baseUomCode || ""}`}</BdiText>
                        )}
                        {res.textValue && <BdiText>{res.textValue}</BdiText>}
                        {res.booleanValue !== null && res.booleanValue !== undefined && (
                          <QualityStatusBadge status={res.booleanValue ? "Passed" : "Failed"} />
                        )}
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem" }}>
                        <QualityStatusBadge status={res.passed ? "Passed" : "Failed"} />
                      </td>
                      <td style={{ padding: "0.75rem 0.5rem", color: "#64748b" }}>
                        {res.evidenceReference ? <BdiText>{res.evidenceReference}</BdiText> : "—"}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Surface>
      )}

      {/* Modals */}
      <RecordResultModal
        inspectionId={inspection.id}
        legalEntityId={inspection.legalEntityId}
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
        legalEntityId={inspection.legalEntityId}
        defaultInspectionId={inspection.id}
        isOpen={isRestrictionOpen}
        onClose={() => setIsRestrictionOpen(false)}
      />

      <CreateDispositionModal
        legalEntityId={inspection.legalEntityId}
        defaultInspectionId={inspection.id}
        defaultLotQuantity={inspection.lotQuantity}
        isOpen={isDispositionOpen}
        onClose={() => setIsDispositionOpen(false)}
      />
    </div>
  );
}