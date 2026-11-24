import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { QualityPaginationBar } from "../components/quality-pagination-bar.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityCertificates } from "../hooks/use-quality-queries.ts";
import { useKeysetPagination } from "../hooks/use-keyset-pagination.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  IssueCertificateModal,
  CertificateViewModal
} from "../components/supplier-certificate-modals.tsx";

export function QualityCertificatesPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [isIssueOpen, setIsIssueOpen] = useState(false);
  const [selectedCertificateId, setSelectedCertificateId] = useState<string | null>(null);
  const [isViewOpen, setIsViewOpen] = useState(false);

  const pagination = useKeysetPagination();

  useEffect(() => {
    pagination.reset();
  }, [legalEntityId]);

  const { data: pageData, isLoading } = useQualityCertificates(
    legalEntityId,
    null,
    null,
    null,
    50,
    pagination.cursor
  );

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const certificates = pageData?.items ?? [];

  return (
    <div data-testid="quality-certificates-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.certificates", "شهادات التحليل والمطابقة (COA)")}
        subtitle={t("tabs.certificates", "شهادات التحليل والمطابقة (COA)")}
        primaryAction={
          <Button variant="primary" onPress={() => setIsIssueOpen(true)}>
            + {t("actions.issueCertificate", "إصدار شهادة جودة (COA/COC)")}
          </Button>
        }
      />

      <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        {isLoading ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
        ) : certificates.length === 0 ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("certificates.number", "رقم الشهادة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("certificates.certificateType", "نوع الوثيقة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("certificates.inspectionId", "رقم أمر الفحص")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("certificates.customerReference", "مرجع العميل")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "عرض الشهادة")}</th>
                </tr>
              </thead>
              <tbody>
                {certificates.map((cert) => (
                  <tr key={cert.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                    <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{cert.number}</td>
                    <td style={{ padding: "10px 12px", fontWeight: 600, color: "#0284c7" }}>{cert.certificateType}</td>
                    <td style={{ padding: "10px 12px", color: "#64748b" }}>{cert.inspectionId.slice(0, 8)}</td>
                    <td style={{ padding: "10px 12px", color: "#475569" }}>{cert.customerReference || "-"}</td>
                    <td style={{ padding: "10px 12px" }}>
                      <QualityStatusBadge status={cert.state} />
                    </td>
                    <td style={{ padding: "10px 12px" }}>
                      <Button
                        variant="primary"
                        onPress={() => {
                          setSelectedCertificateId(cert.id);
                          setIsViewOpen(true);
                        }}
                      >
                        📄 {t("actions.printCertificate", "عرض وطباعة الوثيقة")}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <QualityPaginationBar
          hasPrev={pagination.hasPrev}
          hasNext={Boolean(pageData?.nextCursor)}
          onPrev={pagination.goToPrev}
          onNext={() => pagination.goToNext(pageData?.nextCursor ?? null)}
          isLoading={isLoading}
        />
      </Surface>

      {/* Modals */}
      <IssueCertificateModal
        legalEntityId={legalEntityId}
        inspectionId=""
        isOpen={isIssueOpen}
        onClose={() => setIsIssueOpen(false)}
      />

      {selectedCertificateId && (
        <CertificateViewModal
          certificateId={selectedCertificateId}
          isOpen={isViewOpen}
          onClose={() => {
            setIsViewOpen(false);
            setSelectedCertificateId(null);
          }}
        />
      )}
    </div>
  );
}
