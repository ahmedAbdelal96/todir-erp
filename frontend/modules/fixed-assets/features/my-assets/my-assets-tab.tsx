import React from "react";
import { useTranslation } from "react-i18next";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useMyAssignedAssetsQuery } from "../../hooks/use-fixed-assets-queries.ts";
import type { FixedAssetDto } from "../../types.ts";
import {
  UserIcon,
  CornerDownLeftIcon,
  EyeIcon
} from "../../components/fixed-assets-icons.tsx";

export interface MyAssetsTabProps {
  readonly legalEntityId: string;
  readonly onSelectAsset: (asset: FixedAssetDto) => void;
  readonly onReturnAsset: (asset: FixedAssetDto) => void;
}

export function MyAssetsTab({
  legalEntityId,
  onSelectAsset,
  onReturnAsset
}: MyAssetsTabProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const myAssetsQuery = useMyAssignedAssetsQuery(legalEntityId);

  const items = myAssetsQuery.data?.items ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }} data-testid="my-assets-tab">
      <div
        style={{
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <UserIcon size={22} color="#102a43" />
          <div>
            <h3 style={{ margin: 0, fontSize: "1.0625rem", fontWeight: 700, color: "#102a43" }}>
              {t("myAssets.heading", "أصولي وعهد العمل المسندة إليّ (My Assigned Assets)")}
            </h3>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
              {t("myAssets.desc", "كافة الأجهزة والمعدات المسلمة في عهدتك الشخصية للقيام بمهام العمل.")}
            </p>
          </div>
        </div>

        <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "#1e3a8a", backgroundColor: "#eff6ff", padding: "0.375rem 0.75rem", borderRadius: "9999px" }}>
          {items.length} {t("myAssets.itemsInCustody", "أصول في العهدة")}
        </div>
      </div>

      <div
        style={{
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "0.5rem",
          overflow: "hidden"
        }}
      >
        <Table isBordered={false} data-testid="my-assets-table">
          <thead>
            <tr style={{ backgroundColor: "#f8fafc", borderBottom: "1px solid #cbd5e1", textAlign: "start" }}>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.assetCode", "كود الأصل")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.assetName", "اسم الأصل")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.category", "التصنيف")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.location", "الموقع المسجل")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("fields.inServiceDate", "تاريخ الاستلام")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("common.actions", "الإجراءات")}</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6} style={{ textAlign: "center", padding: "2rem", color: "#64748b" }}>
                  {t("myAssets.noAssetsAssigned", "لا توجد أصول أو عهد عمل مسندة إليك حالياً")}
                </td>
              </tr>
            ) : (
              items.map((row) => (
                <tr key={row.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span style={{ fontWeight: 600, color: "#1e3a8a" }}>{row.assetCode}</span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ fontWeight: 600, color: "#0f172a" }}>{row.assetName}</div>
                    {row.serialNumber && <div style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("fields.serialNumberShort", "ر.ت:")} {row.serialNumber}</div>}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.categoryName}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.locationName ?? "-"}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>{row.inServiceDate ?? row.acquisitionDate}</td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <div style={{ display: "flex", gap: "0.5rem" }}>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={() => onSelectAsset(row)}
                        data-testid={`view-my-asset-${row.id}`}
                      >
                        <EyeIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                        {t("drawer.viewDetails", "عرض التفاصيل")}
                      </Button>
                      <Button
                        variant="secondary"
                        size="compact"
                        onClick={() => onReturnAsset(row)}
                        data-testid={`return-my-asset-${row.id}`}
                      >
                        <CornerDownLeftIcon size={14} style={{ marginInlineEnd: "0.25rem" }} />
                        {t("custody.returnRequest", "طلب إرجاع")}
                      </Button>
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
