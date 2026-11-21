import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { LinkGenealogyDialog } from "../features/genealogy/link-genealogy-dialog.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { useForwardGenealogyQuery, useReverseGenealogyQuery } from "../hooks/use-manufacturing.ts";
import { Button } from "../../../components/primitives/button.tsx";

export function GenealogyPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();

  const [direction, setDirection] = useState<"forward" | "reverse">("reverse");
  const [searchId, setSearchId] = useState("");
  const [activeQueryId, setActiveQueryId] = useState("");
  const [isLinkOpen, setIsLinkOpen] = useState(false);

  const forwardQuery = useForwardGenealogyQuery(activeQueryId);
  const reverseQuery = useReverseGenealogyQuery(activeQueryId);

  const results = direction === "forward" ? forwardQuery.data ?? [] : reverseQuery.data ?? [];
  const isLoading = direction === "forward" ? forwardQuery.isLoading : reverseQuery.isLoading;

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchId.trim()) {
      setActiveQueryId(searchId.trim());
    }
  };

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  return (
    <div
      data-testid="manufacturing-genealogy-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("genealogy.title", "شجرة تتبع التشغيلات (Genealogy)")}
        subtitle={t("genealogy.subtitle", "تتبع متسلسل للمواد الخام المستهلكة والتشغيلات المخرجة للأصناف التامة")}
        primaryAction={
          <Button variant="primary" onClick={() => setIsLinkOpen(true)}>
            + {t("genealogy.linkGenealogy", "ربط تشغيلات")}
          </Button>
        }
      />

      {/* Direction & Search Form */}
      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "16px",
          marginBottom: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <div style={{ display: "flex", gap: "12px", marginBottom: "12px" }}>
          <button
            type="button"
            onClick={() => setDirection("reverse")}
            style={{
              padding: "8px 16px",
              borderRadius: "6px",
              border: "none",
              fontSize: "14px",
              fontWeight: direction === "reverse" ? 700 : 500,
              background: direction === "reverse" ? "#102a43" : "#f1f5f9",
              color: direction === "reverse" ? "#ffffff" : "#475569",
              cursor: "pointer"
            }}
          >
            {t("genealogy.reverseGenealogy", "تتبع عكسي (من أي خامات صُنعت هذه التشغيلة؟)")}
          </button>
          <button
            type="button"
            onClick={() => setDirection("forward")}
            style={{
              padding: "8px 16px",
              borderRadius: "6px",
              border: "none",
              fontSize: "14px",
              fontWeight: direction === "forward" ? 700 : 500,
              background: direction === "forward" ? "#102a43" : "#f1f5f9",
              color: direction === "forward" ? "#ffffff" : "#475569",
              cursor: "pointer"
            }}
          >
            {t("genealogy.forwardGenealogy", "تتبع أمامي (أين استهلكت هذه التشغيلة؟)")}
          </button>
        </div>

        <form onSubmit={handleSearch} style={{ display: "flex", gap: "8px" }}>
          <input
            type="text"
            value={searchId}
            onChange={(e) => setSearchId(e.target.value)}
            placeholder={direction === "reverse" ? t("genealogy.searchOutputLotPlaceholder") : t("genealogy.searchInputLotPlaceholder")}
            style={{
              flex: 1,
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px"
            }}
          />
          <Button type="submit" variant="primary">
            {t("genealogy.searchAndTrace")}
          </Button>
        </form>
      </div>

      {/* Results Tree / Table */}
      <div
        style={{
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "16px",
          boxShadow: "0 1px 3px rgba(0,0,0,0.05)"
        }}
      >
        <h3 style={{ margin: "0 0 12px", fontSize: "15px", fontWeight: 700, color: "#102a43" }}>
          {t("genealogy.linksRecord")}
        </h3>

        {!activeQueryId ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {t("genealogy.enterPrompt")}
          </div>
        ) : results.length === 0 ? (
          <div style={{ padding: "32px 0", textAlign: "center", color: "#64748b" }}>
            {isLoading ? t("common.loading", "جاري التحميل...") : t("genealogy.noLinks")}
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "14px", textAlign: "right" }}>
            <thead>
              <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", color: "#475569" }}>
                <th style={{ padding: "12px 16px" }}>{t("genealogy.outputLot", "تشغيلة المنتج التام")}</th>
                <th style={{ padding: "12px 16px" }}>{t("genealogy.inputLot", "تشغيلة الخام")}</th>
                <th style={{ padding: "12px 16px" }}>{t("genealogy.consumedQty")}</th>
                <th style={{ padding: "12px 16px" }}>{t("genealogy.producedQty")}</th>
                <th style={{ padding: "12px 16px" }}>{t("genealogy.certainty", "درجة التأكيد")}</th>
              </tr>
            </thead>
            <tbody>
              {results.map((item, idx) => (
                <tr key={idx} style={{ borderBottom: "1px solid #f1f5f9" }}>
                  <td style={{ padding: "12px 16px", fontWeight: 700, color: "#0284c7" }}>
                    {item.outputLotId ?? t("genealogy.unspecified")}
                  </td>
                  <td style={{ padding: "12px 16px", fontWeight: 600 }}>
                    {item.inputLotId ?? t("genealogy.unspecified")}
                  </td>
                  <td style={{ padding: "12px 16px" }}>{item.inputQuantity}</td>
                  <td style={{ padding: "12px 16px" }}>{item.outputQuantity}</td>
                  <td style={{ padding: "12px 16px" }}>
                    <span
                      style={{
                        padding: "3px 8px",
                        borderRadius: "4px",
                        fontSize: "12px",
                        fontWeight: 600,
                        background: item.certainty === "Exact" ? "#dcfce7" : "#fef3c7",
                        color: item.certainty === "Exact" ? "#15803d" : "#b45309"
                      }}
                    >
                      {item.certainty === "Exact" ? t("genealogy.exactAttribution", "إسناد مؤكد") : item.certainty}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <LinkGenealogyDialog
        isOpen={isLinkOpen}
        onClose={() => setIsLinkOpen(false)}
        orderId={activeQueryId || undefined}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
