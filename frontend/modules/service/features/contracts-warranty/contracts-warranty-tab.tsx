import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { PartyPicker } from "../../../directory/public.ts";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  ShieldCheckIcon,
  PlusIcon,
  CheckCircleIcon
} from "../../components/service-icons.tsx";
import {
  useContractsQuery,
  useWarrantiesQuery,
  useSlaProfilesQuery,
  useResolveEntitlementMutation,
  useInstalledBaseQuery
} from "../../hooks/use-service-queries.ts";
import { NewContractModal } from "../modals/new-contract-modal.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";

export interface ContractsWarrantyTabProps {
  readonly legalEntityId: string;
}

export function ContractsWarrantyTab({ legalEntityId }: ContractsWarrantyTabProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [activeSection, setActiveSection] = useState<"contracts" | "warranties" | "entitlement">("contracts");
  const [isNewContractOpen, setIsNewContractOpen] = useState(false);

  // Entitlement checker state
  const [checkerCustomerPartyId, setCheckerCustomerPartyId] = useState("");
  const [checkerInstalledBaseId, setCheckerInstalledBaseId] = useState("");
  const [checkerLaborAmount, setCheckerLaborAmount] = useState("300");
  const [checkerPartsAmount, setCheckerPartsAmount] = useState("500");
  const [entitlementError, setEntitlementError] = useState<string | null>(null);

  const contractsQuery = useContractsQuery(legalEntityId, { pageSize: 50 });
  const warrantiesQuery = useWarrantiesQuery(legalEntityId, { pageSize: 50 });
  const slaProfilesQuery = useSlaProfilesQuery(legalEntityId);
  const resolveMutation = useResolveEntitlementMutation();
  const installedBaseQuery = useInstalledBaseQuery(legalEntityId, { pageSize: 100 });
  const installedBaseItems = installedBaseQuery.data?.items ?? [];

  const contracts = contractsQuery.data?.items ?? [];
  const warranties = warrantiesQuery.data?.items ?? [];

  const handleCheckEntitlement = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!checkerCustomerPartyId.trim() && !checkerInstalledBaseId.trim()) {
      setEntitlementError(t("contracts.insufficientCriteria", "يجب تحديد معرف العميل أو معرف المعدة للتحقق من التغطية."));
      return;
    }

    try {
      setEntitlementError(null);
      await resolveMutation.mutateAsync({
        legalEntityId,
        customerPartyId: checkerCustomerPartyId.trim() || undefined,
        installedBaseId: checkerInstalledBaseId.trim() || undefined,
        asOfDate: new Date().toISOString().slice(0, 10),
        lines: [
          { chargeCategory: "Labor", amount: Number(checkerLaborAmount) || 0, currencyCode: "EGP", quantity: 1 },
          { chargeCategory: "Parts", amount: Number(checkerPartsAmount) || 0, currencyCode: "EGP", quantity: 1 }
        ]
      });
    } catch (err: unknown) {
      setEntitlementError(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  const entitlementResult = resolveMutation.data;

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
                      <td>{formatDate(w.startDateUtc || w.startDate, { locale: "ar-EG" })}</td>
                      <td>{formatDate(w.endDateUtc || w.endDate, { locale: "ar-EG" })}</td>
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
          <form onSubmit={handleCheckEntitlement} style={{ display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "800px" }}>
            {entitlementError && (
              <div
                style={{
                  padding: "0.75rem 1rem",
                  borderRadius: "var(--radius-sm, 0.375rem)",
                  backgroundColor: "#fef2f2",
                  border: "1px solid #fecaca",
                  color: "#b91c1c",
                  fontSize: "0.875rem"
                }}
              >
                {entitlementError}
              </div>
            )}

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <PartyPicker
                label={t("requests.customerName")}
                value={checkerCustomerPartyId || undefined}
                onChange={(partyId) => setCheckerCustomerPartyId(partyId)}
                roleFilter="Customer"
                enableQuickCreate={false}
                placeholder={t("requests.customerPlaceholder")}
              />

              <div>
                <label style={{ display: "flex", flexDirection: "column", gap: "0.375rem", fontSize: "0.8125rem", fontWeight: 600 }}>
                  {t("installedBase.title")}
                  <Select
                    value={checkerInstalledBaseId}
                    onChange={setCheckerInstalledBaseId}
                    options={[
                      { value: "", label: "—" },
                      ...installedBaseItems.map((item) => ({
                        value: item.id,
                        label: `${item.name} · ${item.serialNumber ?? "—"}`
                      }))
                    ]}
                  />
                </label>
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                  {t("contracts.testLaborAmount", "قيمة العمل الفني التقديرية")}
                </label>
                <TextField
                  value={checkerLaborAmount}
                  onChange={(val) => setCheckerLaborAmount(val)}
                  placeholder="300"
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                  {t("contracts.testPartsAmount", "قيمة قطع الغيار التقديرية")}
                </label>
                <TextField
                  value={checkerPartsAmount}
                  onChange={(val) => setCheckerPartsAmount(val)}
                  placeholder="500"
                />
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-start" }}>
              <Button
                variant="primary"
                type="submit"
                isLoading={resolveMutation.isPending}
                startIcon={<CheckCircleIcon size={16} />}
              >
                {t("contracts.verifyEntitlement")}
              </Button>
            </div>

            {entitlementResult && (
              <div
                style={{
                  marginTop: "1rem",
                  padding: "1rem",
                  borderRadius: "var(--radius-md, 0.5rem)",
                  backgroundColor: "#ffffff",
                  border: "1px solid var(--color-border-default, #cbd5e1)"
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.75rem" }}>
                  <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                    <strong>{t("contracts.slaState", "حالة اتفاقية SLA:")}</strong>
                    <ServiceLifecycleBadge status={entitlementResult.slaState} size="compact" />
                  </div>

                  <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>
                    {entitlementResult.contractId && (
                      <span style={{ marginRight: "0.75rem" }}>
                        {t("contracts.contractId", "العقد:")} {entitlementResult.contractId.slice(0, 8)}
                      </span>
                    )}
                    {entitlementResult.warrantyId && (
                      <span>
                        {t("contracts.warrantyId", "الضمان:")} {entitlementResult.warrantyId.slice(0, 8)}
                      </span>
                    )}
                  </div>
                </div>

                <Table isBordered isStriped>
                  <thead>
                    <tr>
                      <th>{t("billing.chargeCategory")}</th>
                      <th>{t("contracts.coverageSource", "المصدر")}</th>
                      <th>{t("contracts.coveredStatus", "التغطية")}</th>
                      <th>{t("billing.coveredAmount")}</th>
                      <th>{t("billing.customerAmount")}</th>
                      <th>{t("contracts.explanation", "التفسير المالي")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entitlementResult.lines.map((line, idx) => {
                      const categoryLabel =
                        line.chargeCategory === "Labor" ? t("billing.types.labor", "ساعات عمل فني") :
                        line.chargeCategory === "Parts" ? t("billing.types.parts", "قطع غيار ومواد مستهلكة") :
                        line.chargeCategory === "Travel" ? t("billing.types.travel", "بدل انتقال ومسافة") :
                        line.chargeCategory;

                      const sourceLabel =
                        line.coverageSource === "Contract" ? t("contracts.sources.contract", "عقد صيانة") :
                        line.coverageSource === "Warranty" ? t("contracts.sources.warranty", "ضمان معتمد") :
                        line.coverageSource === "None" ? t("contracts.sources.none", "بدون تغطية") :
                        line.coverageSource;

                      const explanationLabel =
                        line.explanation === "included_in_contract" ? t("contracts.explanations.includedInContract", "مشمول ضمن بنود العقد الساري") :
                        line.explanation === "warranty_active" ? t("contracts.explanations.warrantyActive", "مشمول ضمن فترة الضمان") :
                        line.explanation === "out_of_scope" ? t("contracts.explanations.outOfScope", "خارج نطاق التغطية التعاقدية") :
                        line.explanation;

                      return (
                        <tr key={idx}>
                          <td><strong>{categoryLabel}</strong></td>
                          <td>{sourceLabel}</td>
                          <td>
                            <span
                              style={{
                                color: line.covered ? "#059669" : "#dc2626",
                                fontWeight: 600
                              }}
                            >
                              {line.covered ? t("contracts.covered", "مغطى") : t("contracts.notCovered", "غير مغطى")}
                            </span>
                          </td>
                          <td>{line.coveredAmount.toLocaleString()}</td>
                          <td><strong>{line.customerAmount.toLocaleString()}</strong></td>
                          <td style={{ fontSize: "0.8125rem", color: "#475569" }}>{explanationLabel}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </Table>
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
