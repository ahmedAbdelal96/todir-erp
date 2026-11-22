import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { ManufacturingHeader } from "../components/manufacturing-header.tsx";
import { ManufacturingStatusBadge } from "../components/manufacturing-status-badge.tsx";
import { NoLegalEntityPrompt } from "../components/no-legal-entity-prompt.tsx";
import { useManufacturingScope } from "../hooks/use-manufacturing-scope.ts";
import { CreatePlantDialog } from "../features/definitions/create-plant-dialog.tsx";
import { CreateBomDialog } from "../features/definitions/create-bom-dialog.tsx";
import { CreateFormulaDialog } from "../features/definitions/create-formula-dialog.tsx";
import { CreateRoutingDialog } from "../features/definitions/create-routing-dialog.tsx";
import { CreateWorkCenterDialog } from "../features/definitions/create-work-center-dialog.tsx";
import { CreateDefinitionDialog } from "../features/definitions/create-definition-dialog.tsx";
import { CreateSubstitutionGroupDialog } from "../features/definitions/create-substitution-group-dialog.tsx";
import {
  usePlantsQuery,
  useBomsQuery,
  useFormulasQuery,
  useRoutingsQuery,
  useWorkCentersQuery,
  useDefinitionsQuery,
  useSubstitutionGroupsQuery
} from "../hooks/use-manufacturing.ts";
import { useCatalogProducts } from "../hooks/use-catalog-products.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { BdiText } from "../../../components/formatting/bidi-text.tsx";

type DefinitionTab = "plants" | "boms" | "formulas" | "routings" | "workCenters" | "definitions" | "substitutes";

export function DefinitionsPage(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const { legalEntityId, isReady } = useManufacturingScope();
  const { getProductDisplayName, getProductSku } = useCatalogProducts();

  const [activeTab, setActiveTab] = useState<DefinitionTab>("definitions");

  // Modal triggers
  const [isPlantOpen, setIsPlantOpen] = useState(false);
  const [isBomOpen, setIsBomOpen] = useState(false);
  const [isFormulaOpen, setIsFormulaOpen] = useState(false);
  const [isRoutingOpen, setIsRoutingOpen] = useState(false);
  const [isWorkCenterOpen, setIsWorkCenterOpen] = useState(false);
  const [isDefOpen, setIsDefOpen] = useState(false);
  const [isSubstituteOpen, setIsSubstituteOpen] = useState(false);

  // Queries
  const { data: plants = [] } = usePlantsQuery(legalEntityId);
  const { data: boms = [] } = useBomsQuery();
  const { data: formulas = [] } = useFormulasQuery();
  const { data: routings = [] } = useRoutingsQuery();
  const { data: workCenters = [] } = useWorkCentersQuery();
  const { data: definitions = [] } = useDefinitionsQuery(legalEntityId);
  const { data: subGroups = [] } = useSubstitutionGroupsQuery();

  if (!isReady) {
    return <NoLegalEntityPrompt />;
  }

  const getPrimaryActionForTab = () => {
    switch (activeTab) {
      case "plants":
        return <Button variant="primary" onClick={() => setIsPlantOpen(true)}>+ {t("definitions.addPlant")}</Button>;
      case "boms":
        return <Button variant="primary" onClick={() => setIsBomOpen(true)}>+ {t("definitions.addBom")}</Button>;
      case "formulas":
        return <Button variant="primary" onClick={() => setIsFormulaOpen(true)}>+ {t("definitions.addFormula")}</Button>;
      case "routings":
        return <Button variant="primary" onClick={() => setIsRoutingOpen(true)}>+ {t("definitions.addRouting")}</Button>;
      case "workCenters":
        return <Button variant="primary" onClick={() => setIsWorkCenterOpen(true)}>+ {t("definitions.addWorkCenter")}</Button>;
      case "definitions":
        return <Button variant="primary" onClick={() => setIsDefOpen(true)}>+ {t("definitions.addDefinition")}</Button>;
      case "substitutes":
        return <Button variant="primary" onClick={() => setIsSubstituteOpen(true)}>+ {t("definitions.addSubstituteGroup")}</Button>;
    }
  };

  return (
    <div
      data-testid="manufacturing-definitions-page"
      style={{
        padding: "1.25rem 1.5rem",
        width: "100%",
        maxWidth: "1600px",
        margin: "0 auto",
        boxSizing: "border-box"
      }}
    >
      <ManufacturingHeader
        title={t("definitions.title")}
        subtitle={t("definitions.subtitle")}
        primaryAction={getPrimaryActionForTab()}
      />

      {/* Sub-Tab Selector */}
      <div
        style={{
          display: "flex",
          gap: "8px",
          background: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "8px",
          padding: "8px 12px",
          marginBottom: "16px",
          overflowX: "auto"
        }}
      >
        {[
          { id: "definitions" as const, label: t("definitions.definitionsTab"), count: definitions.length },
          { id: "boms" as const, label: t("definitions.bomsTab"), count: boms.length },
          { id: "formulas" as const, label: t("definitions.formulasTab"), count: formulas.length },
          { id: "routings" as const, label: t("definitions.routingsTab"), count: routings.length },
          { id: "workCenters" as const, label: t("definitions.workCentersTab"), count: workCenters.length },
          { id: "plants" as const, label: t("definitions.plantsTab"), count: plants.length },
          { id: "substitutes" as const, label: t("definitions.substitutesTab"), count: subGroups.length }
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              padding: "8px 16px",
              borderRadius: "6px",
              border: "none",
              fontSize: "14px",
              fontWeight: activeTab === tab.id ? 700 : 500,
              background: activeTab === tab.id ? "#102a43" : "#f1f5f9",
              color: activeTab === tab.id ? "#ffffff" : "#475569",
              cursor: "pointer",
              whiteSpace: "nowrap"
            }}
          >
            {tab.label} ({tab.count})
          </button>
        ))}
      </div>

      {/* Tab Panels */}
      <div style={{ background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "16px" }}>
        {/* Definitions Tab */}
        {activeTab === "definitions" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.definitionsTab")}
            </h3>
            {definitions.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noDefinitions")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("orders.plant")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.product")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("definitions.mode")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.validFrom")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("definitions.expectedYield")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {definitions.map((def) => {
                    const plantName = plants.find((p) => p.id === def.plantId)?.name ?? def.plantId.slice(0, 8);
                    const productName = getProductDisplayName(def.productVariantId);
                    const sku = getProductSku(def.productVariantId);
                    return (
                      <tr key={def.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px", fontWeight: 600 }}>{plantName}</td>
                        <td style={{ padding: "10px 12px" }}>
                          <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                            <span style={{ fontWeight: 600, color: "#102a43" }}>{productName}</span>
                            {sku && <span style={{ fontSize: "11px", color: "#64748b" }}><BdiText>{`${t("common.sku")}: ${sku}`}</BdiText></span>}
                          </div>
                        </td>
                        <td style={{ padding: "10px 12px" }}>{def.manufacturingMode}</td>
                        <td style={{ padding: "10px 12px" }}>{def.validFrom.slice(0, 10)}</td>
                        <td style={{ padding: "10px 12px" }}>{((def.expectedYield ?? 1) * 100).toFixed(0)}%</td>
                        <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={def.state} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* BOMs Tab */}
        {activeTab === "boms" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.bomsTab")}
            </h3>
            {boms.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noBoms")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.product")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {boms.map((b) => {
                    const productName = getProductDisplayName(b.productVariantId);
                    const sku = getProductSku(b.productVariantId);
                    return (
                      <tr key={b.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{b.code}</td>
                        <td style={{ padding: "10px 12px" }}>{b.name}</td>
                        <td style={{ padding: "10px 12px" }}>
                          <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                            <span style={{ fontWeight: 600, color: "#102a43" }}>{productName}</span>
                            {sku && <span style={{ fontSize: "11px", color: "#64748b" }}><BdiText>{`${t("common.sku")}: ${sku}`}</BdiText></span>}
                          </div>
                        </td>
                        <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={b.state} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Formulas Tab */}
        {activeTab === "formulas" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.formulasTab")}
            </h3>
            {formulas.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noFormulas")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.product")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {formulas.map((f) => {
                    const productName = getProductDisplayName(f.productVariantId);
                    const sku = getProductSku(f.productVariantId);
                    return (
                      <tr key={f.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{f.code}</td>
                        <td style={{ padding: "10px 12px" }}>{f.name}</td>
                        <td style={{ padding: "10px 12px" }}>
                          <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                            <span style={{ fontWeight: 600, color: "#102a43" }}>{productName}</span>
                            {sku && <span style={{ fontSize: "11px", color: "#64748b" }}><BdiText>{`${t("common.sku")}: ${sku}`}</BdiText></span>}
                          </div>
                        </td>
                        <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={f.state} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Routings Tab */}
        {activeTab === "routings" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.routingsTab")}
            </h3>
            {routings.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noRoutings")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {routings.map((r) => (
                    <tr key={r.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{r.code}</td>
                      <td style={{ padding: "10px 12px" }}>{r.name}</td>
                      <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={r.state} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Work Centers Tab */}
        {activeTab === "workCenters" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.workCentersTab")}
            </h3>
            {workCenters.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noWorkCenters")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("definitions.capacityModel")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.efficiency")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {workCenters.map((w) => (
                    <tr key={w.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{w.code}</td>
                      <td style={{ padding: "10px 12px" }}>{w.name}</td>
                      <td style={{ padding: "10px 12px" }}>{w.capacityModel}</td>
                      <td style={{ padding: "10px 12px" }}>{((w.defaultEfficiency ?? 1) * 100).toFixed(0)}%</td>
                      <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={w.state} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Plants Tab */}
        {activeTab === "plants" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.plantsTab")}
            </h3>
            {plants.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noPlants")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.timeZone")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("orders.stateLabel")}</th>
                  </tr>
                </thead>
                <tbody>
                  {plants.map((p) => (
                    <tr key={p.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{p.code}</td>
                      <td style={{ padding: "10px 12px" }}>{p.name}</td>
                      <td style={{ padding: "10px 12px" }}>{p.timeZone}</td>
                      <td style={{ padding: "10px 12px" }}><ManufacturingStatusBadge state={p.state} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Substitutes Tab */}
        {activeTab === "substitutes" && (
          <div>
            <h3 style={{ margin: "0 0 12px", fontSize: "16px", fontWeight: 700, color: "#102a43" }}>
              {t("definitions.substitutesTab")}
            </h3>
            {subGroups.length === 0 ? (
              <p style={{ color: "#64748b" }}>{t("definitions.noSubstitutes")}</p>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "right" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "8px 12px" }}>{t("common.code")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("common.name")}</th>
                    <th style={{ padding: "8px 12px" }}>{t("definitions.substitutionPolicy")}</th>
                  </tr>
                </thead>
                <tbody>
                  {subGroups.map((sg) => (
                    <tr key={sg.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#0284c7" }}>{sg.code}</td>
                      <td style={{ padding: "10px 12px" }}>{sg.name}</td>
                      <td style={{ padding: "10px 12px" }}>{sg.policy}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </div>

      <CreatePlantDialog
        isOpen={isPlantOpen}
        onClose={() => setIsPlantOpen(false)}
        legalEntityId={legalEntityId ?? ""}
      />
      <CreateBomDialog
        isOpen={isBomOpen}
        onClose={() => setIsBomOpen(false)}
      />
      <CreateFormulaDialog
        isOpen={isFormulaOpen}
        onClose={() => setIsFormulaOpen(false)}
      />
      <CreateRoutingDialog
        isOpen={isRoutingOpen}
        onClose={() => setIsRoutingOpen(false)}
      />
      <CreateWorkCenterDialog
        isOpen={isWorkCenterOpen}
        onClose={() => setIsWorkCenterOpen(false)}
        legalEntityId={legalEntityId ?? ""}
      />
      <CreateDefinitionDialog
        isOpen={isDefOpen}
        onClose={() => setIsDefOpen(false)}
        legalEntityId={legalEntityId ?? ""}
      />
      <CreateSubstitutionGroupDialog
        isOpen={isSubstituteOpen}
        onClose={() => setIsSubstituteOpen(false)}
      />
    </div>
  );
}
