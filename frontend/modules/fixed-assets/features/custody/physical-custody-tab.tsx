import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useFixedAssetLocationsQuery,
  useVerificationCampaignsQuery,
  useCloseVerificationCampaignMutation,
  useFixedAssetsQuery
} from "../../hooks/use-fixed-assets-queries.ts";
import type { FixedAssetDto } from "../../types.ts";
import {
  MapPinIcon,
  PlusIcon,
  ShieldCheckIcon,
  ArrowRightLeftIcon,
  QrCodeIcon,
  CheckCircle2Icon
} from "../../components/fixed-assets-icons.tsx";

export interface PhysicalCustodyTabProps {
  readonly legalEntityId: string;
  readonly onOpenNewLocationModal: () => void;
  readonly onOpenNewCampaignModal: () => void;
  readonly onOpenMassTransferModal: () => void;
  readonly onOpenRecordFindingModal: (campaignId: string) => void;
  readonly onSelectAsset: (asset: FixedAssetDto) => void;
}

export function PhysicalCustodyTab({
  legalEntityId,
  onOpenNewLocationModal,
  onOpenNewCampaignModal,
  onOpenMassTransferModal,
  onOpenRecordFindingModal,
  onSelectAsset
}: PhysicalCustodyTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");

  const [scanBarcode, setScanBarcode] = useState("");
  const locationsQuery = useFixedAssetLocationsQuery(legalEntityId);
  const campaignsQuery = useVerificationCampaignsQuery(legalEntityId);
  const closeCampaignMutation = useCloseVerificationCampaignMutation();

  // Asset search for barcode lookup
  const assetsQuery = useFixedAssetsQuery(legalEntityId, {
    search: scanBarcode.trim() ? scanBarcode.trim() : undefined,
    pageSize: 5
  });

  const locations = locationsQuery.data?.items ?? [];
  const campaigns = campaignsQuery.data ?? [];
  const scannedAssets = scanBarcode.trim() ? assetsQuery.data?.items ?? [] : [];

  const handleCloseCampaign = async (campaignId: string) => {
    try {
      await closeCampaignMutation.mutateAsync(campaignId);
    } catch {
      // handled by mutation
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }} data-testid="physical-custody-tab">
      {/* Top Controls & Scan Barcode Tool */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "1.5fr 1fr",
          gap: "1rem",
          alignItems: "stretch"
        }}
      >
        {/* Quick QR/Barcode Scan Tool */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "0.5rem",
            display: "flex",
            flexDirection: "column",
            gap: "0.75rem"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <QrCodeIcon size={20} color="#102a43" />
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("custody.quickScanHeading", "أداة المسح والتحقق الميداني السريع (QR / Barcode)")}
            </h3>
          </div>
          <TextField
            value={scanBarcode}
            onChange={setScanBarcode}
            placeholder={t("custody.scanPlaceholder", "امسح أو اكتب الباركود / كود الأصل للبحث الفوري...")}
            data-testid="scan-barcode-input"
          />

          {scanBarcode.trim() && scannedAssets.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", marginTop: "0.5rem" }}>
              {scannedAssets.map((asset) => (
                <div
                  key={asset.id}
                  onClick={() => onSelectAsset(asset)}
                  style={{
                    padding: "0.75rem",
                    backgroundColor: "#f8fafc",
                    border: "1px solid #cbd5e1",
                    borderRadius: "0.375rem",
                    cursor: "pointer",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center"
                  }}
                  data-testid={`scanned-asset-result-${asset.id}`}
                >
                  <div>
                    <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#1e3a8a" }}>
                      {asset.assetName} ({asset.assetCode})
                    </div>
                    <div style={{ fontSize: "0.75rem", color: "#64748b" }}>
                      {t("fields.custodian", "العهدة")}: {asset.custodianId ?? "-"} | {t("fields.location", "الموقع")}: {asset.locationName ?? "-"}
                    </div>
                  </div>
                  <Button variant="secondary" size="compact">
                    {t("drawer.viewDetails", "عرض الملف")}
                  </Button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Action Shortcuts */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "#ffffff",
            border: "1px solid #cbd5e1",
            borderRadius: "0.5rem",
            display: "flex",
            flexDirection: "column",
            justifyContent: "space-between",
            gap: "0.75rem"
          }}
        >
          <div>
            <h3 style={{ margin: "0 0 0.5rem", fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("custody.quickActionsHeading", "عمليات العهدة والجرد")}
            </h3>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "#64748b" }}>
              {t("custody.quickActionsDesc", "تنفيذ النقل الجماعي للعهدة أو إطلاق حملة تدقيق ميداني جديدة.")}
            </p>
          </div>

          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            <Button variant="primary" onClick={onOpenMassTransferModal} data-testid="mass-transfer-btn">
              <ArrowRightLeftIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
              {t("custody.massTransfer", "نقل جماعي للعهدة")}
            </Button>
            <Button variant="secondary" onClick={onOpenNewCampaignModal} data-testid="new-campaign-btn">
              <ShieldCheckIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
              {t("custody.newCampaign", "حملة جرد جديدة")}
            </Button>
            <Button variant="secondary" onClick={onOpenNewLocationModal} data-testid="new-location-btn">
              <PlusIcon size={16} style={{ marginInlineEnd: "0.375rem" }} />
              {t("custody.newLocation", "إضافة موقع")}
            </Button>
          </div>
        </div>
      </div>

      {/* Locations Sites Grid */}
      <div
        style={{
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <MapPinIcon size={20} color="#102a43" />
            <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
              {t("custody.locationsHeading", "المواقع والمستودعات المعتمدة")}
            </h3>
          </div>
          <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
            {locations.length} {t("custody.locationsCount", "موقع مسجل")}
          </span>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: "1rem" }}>
          {locations.map((loc) => (
            <div
              key={loc.id}
              style={{
                padding: "0.875rem",
                backgroundColor: "#f8fafc",
                border: "1px solid #cbd5e1",
                borderRadius: "0.375rem"
              }}
              data-testid={`location-card-${loc.id}`}
            >
              <div style={{ fontWeight: 600, fontSize: "0.875rem", color: "#0f172a" }}>{loc.locationName}</div>
              <div style={{ fontSize: "0.75rem", color: "#64748b", marginTop: "0.25rem" }}>
                {loc.code} ({loc.locationType})
              </div>
              <div style={{ fontSize: "0.75rem", color: "#1e3a8a", fontWeight: 500, marginTop: "0.5rem" }}>
                {loc.assetCount ?? 0} {t("custody.assetsInLocation", "أصل مسجل بالموقع")}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Verification Campaigns Table */}
      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <div style={{ padding: "1rem", borderBottom: "1px solid #e2e8f0", display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <ShieldCheckIcon size={20} color="#102a43" />
          <h3 style={{ margin: 0, fontSize: "1rem", fontWeight: 700, color: "#102a43" }}>
            {t("custody.campaignsHeading", "حملات الجرد الفعلي والتدقيق الميداني")}
          </h3>
        </div>

        <Table isBordered={false} data-testid="verification-campaigns-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.campaignName", "اسم الحملة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.auditScope", "النطاق")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.status", "الحالة")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
            </tr>
          </thead>
          <tbody>
            {campaigns.length === 0 ? (
              <tr>
                <td colSpan={4} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                  {t("custody.noCampaigns", "لا توجد حملات جرد حالية")}
                </td>
              </tr>
            ) : (
              campaigns.map((row) => (
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{row.name}</span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{t("custody.targetAssetsCount", "{{count}} أصل مستهدف", { count: row.lineCount })}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "0.2rem 0.5rem",
                        borderRadius: "9999px",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: row.status === "Active" ? "#eff6ff" : "#f1f5f9",
                        color: row.status === "Active" ? "#1e40af" : "#475569"
                      }}
                    >
                      {row.status === "Active" ? t("custody.campaignOpen", "جارية (Active)") : t("custody.campaignClosed", "مكتملة (Completed)")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.5rem" }}>
                      {row.status === "Active" && (
                        <>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => onOpenRecordFindingModal(row.id)}
                            data-testid={`record-finding-btn-${row.id}`}
                          >
                            <QrCodeIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                            {t("custody.recordFinding", "تسجيل فحص")}
                          </Button>
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => handleCloseCampaign(row.id)}
                            isLoading={closeCampaignMutation.isPending}
                            data-testid={`close-campaign-btn-${row.id}`}
                          >
                            <CheckCircle2Icon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                            {t("custody.closeCampaign", "إغلاق الحملة")}
                          </Button>
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
