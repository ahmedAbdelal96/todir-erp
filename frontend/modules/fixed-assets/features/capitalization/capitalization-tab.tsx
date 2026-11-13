import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useCapitalizationCandidatesQuery,
  useCipProjectsQuery,
  useAllocateToCipMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type {
  CapitalizationCandidateDto,
  CipProjectDto
} from "../../types.ts";
import {
  LayersIcon,
  CheckCircle2Icon,
  ArrowRightLeftIcon
} from "../../components/fixed-assets-icons.tsx";

export interface CapitalizationTabProps {
  readonly legalEntityId: string;
  readonly onOpenCapitalizeCipModal: (project: CipProjectDto) => void;
  readonly onOpenSplitCipModal: (project: CipProjectDto) => void;
  readonly onOpenCombineCipModal: () => void;
  readonly onCapitalizeCandidateAsAsset: (candidate: CapitalizationCandidateDto) => void;
}

export function CapitalizationTab({
  legalEntityId,
  onOpenCapitalizeCipModal,
  onOpenSplitCipModal,
  onOpenCombineCipModal,
  onCapitalizeCandidateAsAsset
}: CapitalizationTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  const candidatesQuery = useCapitalizationCandidatesQuery(legalEntityId);
  const cipQuery = useCipProjectsQuery(legalEntityId);
  const allocateMutation = useAllocateToCipMutation(legalEntityId);

  const [selectedCipForAllocation, setSelectedCipForAllocation] = useState<string>("");

  const candidates = (candidatesQuery.data as readonly CapitalizationCandidateDto[] | undefined) ?? [];
  const cipProjects = cipQuery.data?.items ?? [];

  const handleAllocate = async (candidateId: string) => {
    if (!selectedCipForAllocation) return;
    try {
      await allocateMutation.mutateAsync({
        candidateId,
        cipProjectId: selectedCipForAllocation
      });
    } catch {
      // handled by mutation
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }} data-testid="capitalization-tab">
      {/* CIP Projects Overview & Management */}
      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <div
          style={{
            padding: "1rem",
            borderBottom: "1px solid #e2e8f0",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <LayersIcon size={20} color="#102a43" />
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("cip.projectsHeading", "المشاريع الرأسمالية قيد التنفيذ (CIP Projects)")}
            </h3>
          </div>

          <div style={{ display: "flex", gap: "0.5rem" }}>
            {cipProjects.length >= 2 && (
              <Button variant="secondary" size="compact" onClick={onOpenCombineCipModal} data-testid="combine-cip-btn">
                <ArrowRightLeftIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
                {t("cip.combine", "دمج مشاريع")}
              </Button>
            )}
          </div>
        </div>

        <Table isBordered={false} data-testid="cip-projects-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.projectCode", "كود المشروع")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.projectName", "اسم المشروع")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("cip.accumulatedCost", "التكلفة المتراكمة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("cip.dates", "تاريخ الإنجاز المتوقع")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.status", "الحالة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
            </tr>
          </thead>
          <tbody>
            {cipProjects.length === 0 ? (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                  {t("cip.noProjects", "لا توجد مشاريع رأسمالية قيد التنفيذ")}
                </td>
              </tr>
            ) : (
              cipProjects.map((row) => (
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{row.projectCode}</span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ fontWeight: 600, color: "#0f172a" }}>{row.projectName}</div>
                    {row.description && <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{row.description}</div>}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 700, color: "#1e3a8a" }}>
                      {(row.accumulatedCost ?? 0).toLocaleString("ar-EG", { minimumFractionDigits: 2 })} {row.currency ?? ""}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.estimatedCompletionDate ?? "-"}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "0.2rem 0.5rem",
                        borderRadius: "9999px",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: "#eff6ff",
                        color: "#1e40af"
                      }}
                    >
                      {t("states.underConstruction", "قيد التنفيذ")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.375rem" }}>
                      <Button
                        variant="primary"
                        size="compact"
                        onClick={() => onOpenCapitalizeCipModal(row)}
                        data-testid={`capitalize-cip-btn-${row.id}`}
                      >
                        <CheckCircle2Icon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                        {t("cip.capitalize", "رسملة لأصل مشغل")}
                      </Button>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={() => onOpenSplitCipModal(row)}
                        data-testid={`split-cip-btn-${row.id}`}
                      >
                        {t("cip.split", "تقسيم")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </Table>
      </div>

      {/* Capitalization Candidates */}
      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <div
          style={{
            padding: "1rem",
            borderBottom: "1px solid #e2e8f0",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "0.75rem"
          }}
        >
          <div>
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("capitalization.candidatesHeading", "مرشحات الرسملة من المشتريات والمصروفات (Capitalization Candidates)")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
              {t("capitalization.candidatesDesc", "فواتير وأوامر شراء ومصروفات مؤهلة للرسملة وتحويلها إلى أصول ثابتة أو إضافتها إلى CIP.")}
            </p>
          </div>

          {cipProjects.length > 0 && (
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <span style={{ fontSize: "0.8125rem", color: "#334155" }}>
                {t("capitalization.targetCipForAllocation", "مشروع CIP المستهدف للتخصيص:")}
              </span>
              <select
                value={selectedCipForAllocation}
                onChange={(e) => setSelectedCipForAllocation(e.target.value)}
                style={{
                  padding: "0.375rem 0.5rem",
                  fontSize: "0.8125rem",
                  borderRadius: "0.375rem",
                  border: "1px solid #cbd5e1",
                  backgroundColor: "#ffffff"
                }}
                data-testid="target-cip-select"
              >
                <option value="">{t("fields.selectCip", "اختر مشروع CIP...")}</option>
                {cipProjects.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.projectName} ({(p.accumulatedCost ?? 0).toLocaleString("ar-EG")} {p.currency ?? ""})
                  </option>
                ))}
              </select>
            </div>
          )}
        </div>

        <Table isBordered={false} data-testid="capitalization-candidates-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.sourceType", "المصدر")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.description", "البيان")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.amount", "المبلغ")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.status", "الحالة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
            </tr>
          </thead>
          <tbody>
            {candidates.length === 0 ? (
              <tr>
                <td colSpan={5} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                  {t("capitalization.noCandidates", "لا توجد فواتير أو بنود مشتريات معلقة للرسملة")}
                </td>
              </tr>
            ) : (
              candidates.map((row) => (
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{row.sourceReference}</span>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{row.sourceType}</div>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.sourceReference}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#0f172a" }}>
                      {(row.eligibleAmount ?? 0).toLocaleString("ar-EG", { minimumFractionDigits: 2 })} EGP
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "0.2rem 0.5rem",
                        borderRadius: "9999px",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: row.status === "Allocated" ? "#f0fdf4" : "#fff7ed",
                        color: row.status === "Allocated" ? "#16a34a" : "#c2410c"
                      }}
                    >
                      {row.status === "Allocated" ? t("capitalization.allocated", "مخصص") : t("capitalization.unallocated", "بانتظار التخصيص")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.5rem" }}>
                      {row.status !== "Allocated" && (
                        <>
                          <Button
                            variant="primary"
                            size="compact"
                            onClick={() => onCapitalizeCandidateAsAsset(row)}
                            data-testid={`capitalize-candidate-btn-${row.id}`}
                          >
                            {t("capitalization.capitalizeAsAsset", "رسملة كأصل مباشر")}
                          </Button>
                          {cipProjects.length > 0 && (
                            <Button
                              variant="secondary"
                              size="compact"
                              onClick={() => handleAllocate(row.id)}
                              isDisabled={!selectedCipForAllocation}
                              data-testid={`allocate-to-cip-btn-${row.id}`}
                            >
                              {t("capitalization.allocateToCip", "تخصيص للـ CIP")}
                            </Button>
                          )}
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </Table>
      </div>
    </div>
  );
}
