import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  ShieldCheckIcon,
  PlusIcon,
  CheckCircleIcon
} from "../../components/service-icons.tsx";
import {
  useContractsQuery,
  useWarrantiesQuery,
  useSlaProfilesQuery
} from "../../hooks/use-service-queries.ts";
import { NewContractModal } from "../modals/new-contract-modal.tsx";

export interface ContractsWarrantyTabProps {
  readonly legalEntityId: string;
}

export function ContractsWarrantyTab({ legalEntityId }: ContractsWarrantyTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [activeSection, setActiveSection] = useState<"contracts" | "warranties" | "entitlement">("contracts");
  const [isNewContractOpen, setIsNewContractOpen] = useState(false);

  // Entitlement checker state
  const [checkerCustomer, setCheckerCustomer] = useState("");
  const [checkerSerial, setCheckerSerial] = useState("");
  const [entitlementResult, setEntitlementResult] = useState<{
    covered: boolean;
    coverageType: string;
    slaResponseHours: number;
  } | null>(null);

  const contractsQuery = useContractsQuery(legalEntityId, { pageSize: 50 });
  const warrantiesQuery = useWarrantiesQuery(legalEntityId, { pageSize: 50 });
  const slaProfilesQuery = useSlaProfilesQuery(legalEntityId);

  const contracts = contractsQuery.data?.items ?? [];
  const warranties = warrantiesQuery.data?.items ?? [];

  const handleCheckEntitlement = (e: React.FormEvent) => {
    e.preventDefault();
    if (!checkerCustomer.trim() && !checkerSerial.trim()) return;

    // Entitlement verification rule logic
    const matchedContract = contracts.find(
      (c) =>
        checkerCustomer &&
        (c.customerName || c.contractHolderPartyId || "").toLowerCase().includes(checkerCustomer.toLowerCase()) &&
        (c.state === "Active" || c.status === "active")
    );

    const matchedWarranty = warranties.find(
      (w) =>
        checkerSerial &&
        (w.serialNumber || w.subjectId || "").toLowerCase() === checkerSerial.toLowerCase() &&
        (w.state === "Active" || w.status === "active")
    );

    if (matchedContract) {
      setEntitlementResult({
        covered: true,
        coverageType: `Contract: ${matchedContract.contractNumber || matchedContract.id.slice(0, 8)} (${matchedContract.coverageTerms || "Full Support"})`,
        slaResponseHours: 4
      });
    } else if (matchedWarranty) {
      setEntitlementResult({
        covered: true,
        coverageType: `Standard Manufacturer Warranty: ${matchedWarranty.warrantyType}`,
        slaResponseHours: 8
      });
    } else {
      setEntitlementResult({
        covered: false,
        coverageType: "No active contract or warranty found. Standard billable rates apply.",
        slaResponseHours: 24
      });
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Ribbon Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.5rem" }}>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={activeSection === "contracts" ? "primary" : "secondary"}
            onClick={() => setActiveSection("contracts")}
            startIcon={<ShieldCheckIcon size={16} />}
          >
            {t("contracts.contractsTitle")} ({contracts.length})
          </Button>
          <Button
            variant={activeSection === "warranties" ? "primary" : "secondary"}
            onClick={() => setActiveSection("warranties")}
            startIcon={<ShieldCheckIcon size={16} />}
          >
            {t("contracts.warrantiesTitle")} ({warranties.length})
          </Button>
          <Button
            variant={activeSection === "entitlement" ? "primary" : "secondary"}
            onClick={() => setActiveSection("entitlement")}
            startIcon={<CheckCircleIcon size={16} />}
          >
            {t("contracts.entitlementCheckTitle")}
          </Button>
        </div>

        <Button
          variant="primary"
          onClick={() => setIsNewContractOpen(true)}
          startIcon={<PlusIcon size={16} />}
        >
          {t("contracts.newContractTitle")}
        </Button>
      </div>

      {activeSection === "contracts" && (
        <Surface
          title={t("contracts.contractsTitle")}
          description={t("contracts.contractsDesc")}
          variant="card"
        >
          {contractsQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : contracts.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("contracts.noContractsFound")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("contracts.contractNumber")}</th>
                    <th>{t("requests.customerName")}</th>
                    <th>{t("contracts.startDate")}</th>
                    <th>{t("contracts.endDate")}</th>
                    <th>{t("contracts.coverageTerms")}</th>
                    <th>{t("contracts.contractPrice")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {contracts.map((c) => (
                    <tr key={c.id}>
                      <td><strong>{c.contractNumber || c.id.slice(0, 8)}</strong></td>
                      <td>{c.customerName || c.contractHolderPartyId}</td>
                      <td>{new Date(c.startDateUtc || c.startDate).toLocaleDateString()}</td>
                      <td>{new Date(c.endDateUtc || c.endDate).toLocaleDateString()}</td>
                      <td>{c.coverageTerms || "—"}</td>
                      <td>{c.price || c.value ? `${c.price ?? c.value} ${c.currency || c.currencyCode}` : "—"}</td>
                      <td><ServiceLifecycleBadge state={c.state || c.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {activeSection === "warranties" && (
        <Surface
          title={t("contracts.warrantiesTitle")}
          description={t("contracts.warrantiesDesc")}
          variant="card"
        >
          {warrantiesQuery.isLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
              {t("common.loading")}
            </div>
          ) : warranties.length === 0 ? (
            <div style={{ padding: "3rem 1rem", textAlign: "center", color: "#64748b" }}>
              {t("contracts.noWarrantiesFound")}
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <Table isBordered isStriped>
                <thead>
                  <tr>
                    <th>{t("installedBase.serialNumber")}</th>
                    <th>{t("requests.customerName")}</th>
                    <th>{t("contracts.warrantyType")}</th>
                    <th>{t("contracts.startDate")}</th>
                    <th>{t("contracts.endDate")}</th>
                    <th>{t("common.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {warranties.map((w) => (
                    <tr key={w.id}>
                      <td><strong>{w.serialNumber || w.subjectId || w.id.slice(0, 8)}</strong></td>
                      <td>{w.customerName || w.customerPartyId || "—"}</td>
                      <td>{w.warrantyType}</td>
                      <td>{new Date(w.startDateUtc || w.startDate).toLocaleDateString()}</td>
                      <td>{new Date(w.endDateUtc || w.endDate).toLocaleDateString()}</td>
                      <td><ServiceLifecycleBadge state={w.state || w.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          )}
        </Surface>
      )}

      {activeSection === "entitlement" && (
        <Surface
          title={t("contracts.entitlementCheckTitle")}
          description={t("contracts.entitlementCheckDesc")}
          variant="card"
        >
          <form onSubmit={handleCheckEntitlement} style={{ display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "600px" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                  {t("requests.customerName")}
                </label>
                <TextField
                  value={checkerCustomer}
                  onChange={(val) => setCheckerCustomer(val)}
                  placeholder={t("contracts.customerPlaceholder", "مثال: مجموعة الفطيم")}
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                  {t("installedBase.serialNumber")}
                </label>
                <TextField
                  value={checkerSerial}
                  onChange={(val) => setCheckerSerial(val)}
                  placeholder={t("contracts.serialPlaceholder", "مثال: SN-GEN-88301")}
                />
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-start" }}>
              <Button variant="primary" type="submit" startIcon={<CheckCircleIcon size={16} />}>
                {t("contracts.verifyEntitlement")}
              </Button>
            </div>

            {entitlementResult && (
              <div
                style={{
                  marginTop: "1rem",
                  padding: "1rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: entitlementResult.covered ? "#ecfdf5" : "#fffbeb",
                  border: `1px solid ${entitlementResult.covered ? "#a7f3d0" : "#fde68a"}`
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.5rem" }}>
                  <strong style={{ color: entitlementResult.covered ? "#065f46" : "#92400e" }}>
                    {entitlementResult.covered ? t("contracts.activeCoverageConfirmed", "✓ تغطية نشطة مؤكدة") : t("contracts.nonCoveredBillable", "⚠ غير مغطى / خاضع للفوترة")}
                  </strong>
                </div>
                <p style={{ margin: "0.25rem 0", fontSize: "0.875rem", color: "#334155" }}>
                  {entitlementResult.coverageType}
                </p>
                <div style={{ marginTop: "0.5rem", fontSize: "0.8125rem", color: "#64748b" }}>
                  {t("contracts.targetSlaResponse", "زمن الاستجابة المستهدف:")} <strong>{entitlementResult.slaResponseHours} {t("contracts.hours", "ساعات")}</strong>
                </div>
              </div>
            )}
          </form>
        </Surface>
      )}

      {/* New Contract Modal */}
      {isNewContractOpen && (
        <NewContractModal
          isOpen={isNewContractOpen}
          onClose={() => setIsNewContractOpen(false)}
          legalEntityId={legalEntityId}
        />
      )}
    </div>
  );
}
