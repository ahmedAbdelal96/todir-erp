import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useExpenseCategoriesQuery,
  useExpensePoliciesQuery,
  useExpenseRatePoliciesQuery,
  usePublishCategoryVersionMutation,
  usePublishPolicyVersionMutation,
  usePublishRatePolicyMutation
} from "../../hooks/use-expenses-queries.ts";
import {
  NewCategoryModal,
  NewCategoryVersionModal,
  NewPolicyModal,
  NewPolicyVersionModal,
  NewRatePolicyModal,
  ImportExpensesModal
} from "../modals/settings-modals.tsx";

export interface ExpensesSettingsTabProps {
  readonly legalEntityId: string;
}

export function ExpensesSettingsTab({ legalEntityId }: ExpensesSettingsTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const [subtab, setSubtab] = useState<"categories" | "policies" | "ratePolicies" | "imports">("categories");

  const [isNewCategoryOpen, setIsNewCategoryOpen] = useState(false);
  const [newVersionCatId, setNewVersionCatId] = useState<string | null>(null);
  const [isNewPolicyOpen, setIsNewPolicyOpen] = useState(false);
  const [newVersionPolicyId, setNewVersionPolicyId] = useState<string | null>(null);
  const [isNewRateOpen, setIsNewRateOpen] = useState(false);
  const [isImportOpen, setIsImportOpen] = useState(false);

  const categoriesQuery = useExpenseCategoriesQuery(legalEntityId);
  const policiesQuery = useExpensePoliciesQuery(legalEntityId);
  const ratePoliciesQuery = useExpenseRatePoliciesQuery(legalEntityId);

  const publishCatMutation = usePublishCategoryVersionMutation(legalEntityId);
  const publishPolicyMutation = usePublishPolicyVersionMutation(legalEntityId);
  const publishRateMutation = usePublishRatePolicyMutation(legalEntityId);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("settingsTab.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("settingsTab.description")}
            </p>
          </div>

          <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
            {subtab === "categories" && (
              <Button variant="primary" onClick={() => setIsNewCategoryOpen(true)}>
                + {t("settingsTab.newCategory")}
              </Button>
            )}
            {subtab === "policies" && (
              <Button variant="primary" onClick={() => setIsNewPolicyOpen(true)}>
                + {t("settingsTab.newPolicy")}
              </Button>
            )}
            {subtab === "ratePolicies" && (
              <Button variant="primary" onClick={() => setIsNewRateOpen(true)}>
                + {t("settingsTab.newRatePolicy")}
              </Button>
            )}
            {subtab === "imports" && (
              <Button variant="primary" onClick={() => setIsImportOpen(true)}>
                + {t("settingsTab.importExpenses")}
              </Button>
            )}
          </div>
        </div>

        {/* Subtabs Selector */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "2px solid var(--color-border-default, #cbd5e1)", paddingBottom: "0.5rem" }}>
          {[
            { id: "categories", label: t("settingsTab.subtabs.categories") },
            { id: "policies", label: t("settingsTab.subtabs.policies") },
            { id: "ratePolicies", label: t("settingsTab.subtabs.ratePolicies") },
            { id: "imports", label: t("settingsTab.subtabs.imports") }
          ].map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setSubtab(item.id as any)}
              style={{
                padding: "0.5rem 1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "none",
                background: subtab === item.id ? "var(--color-brand-primary, #102a43)" : "transparent",
                color: subtab === item.id ? "#ffffff" : "var(--color-content-primary, #0f172a)",
                fontWeight: 600,
                fontSize: "0.875rem",
                cursor: "pointer"
              }}
            >
              {item.label}
            </button>
          ))}
        </div>

        {/* Subtab 1: Categories */}
        {subtab === "categories" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            {categoriesQuery.isLoading ? (
              <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
            ) : !categoriesQuery.data || categoriesQuery.data.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                {t("settingsTab.emptyCategories")}
              </div>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <th>{t("settingsTab.categoryCode")}</th>
                    <th>{t("settingsTab.categoryName")}</th>
                    <th>{t("common.status")}</th>
                    <th>{t("settingsTab.publishedVersion")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {categoriesQuery.data.map((cat) => (
                    <tr key={cat.id}>
                      <td style={{ fontWeight: 600 }}>{cat.code}</td>
                      <td>{cat.name}</td>
                      <td>{cat.status}</td>
                      <td>{cat.publishedVersionId ? t("settingsTab.publishedActive") : t("settingsTab.draftStatus")}</td>
                      <td>
                        <Button variant="secondary" size="compact" onClick={() => setNewVersionCatId(cat.id)}>
                          {t("settingsTab.newVersionAction")}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </div>
        )}

        {/* Subtab 2: Policies */}
        {subtab === "policies" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            {policiesQuery.isLoading ? (
              <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
            ) : !policiesQuery.data?.items || policiesQuery.data.items.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                {t("settingsTab.emptyPolicies")}
              </div>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <th>{t("settingsTab.policyCode")}</th>
                    <th>{t("settingsTab.policyName")}</th>
                    <th>{t("common.status")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {policiesQuery.data.items.map((pol) => (
                    <tr key={pol.id}>
                      <td style={{ fontWeight: 600 }}>{pol.code}</td>
                      <td>{pol.name}</td>
                      <td>{pol.status}</td>
                      <td>
                        <Button variant="secondary" size="compact" onClick={() => setNewVersionPolicyId(pol.id)}>
                          {t("settingsTab.newRulesAction")}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </div>
        )}

        {/* Subtab 3: Rate Policies */}
        {subtab === "ratePolicies" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            {ratePoliciesQuery.isLoading ? (
              <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
            ) : !ratePoliciesQuery.data?.items || ratePoliciesQuery.data.items.length === 0 ? (
              <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                {t("settingsTab.emptyRatePolicies")}
              </div>
            ) : (
              <Table>
                <thead>
                  <tr>
                    <th>{t("settingsTab.rateKind")}</th>
                    <th>{t("settingsTab.rateValue")}</th>
                    <th>{t("common.currency")}</th>
                    <th>{t("common.status")}</th>
                    <th>{t("settingsTab.effectiveFrom")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {ratePoliciesQuery.data.items.map((rate) => (
                    <tr key={rate.id}>
                      <td style={{ fontWeight: 600 }}>{rate.kind}</td>
                      <td style={{ fontWeight: 600 }}>
                        <bdi>{rate.rate.toFixed(2)}</bdi>
                      </td>
                      <td>{rate.currencyCode}</td>
                      <td>{rate.status}</td>
                      <td>{rate.effectiveFrom}</td>
                      <td>
                        {rate.status !== "Published" && (
                          <Button
                            variant="primary"
                            size="compact"
                            onClick={() => publishRateMutation.mutate({ id: rate.id, request: {} })}
                          >
                            {t("settingsTab.publishAction")}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </div>
        )}

        {/* Subtab 4: Imports */}
        {subtab === "imports" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
              {t("settingsTab.emptyImports")}
            </div>
          </div>
        )}
      </Surface>

      <NewCategoryModal
        isOpen={isNewCategoryOpen}
        onClose={() => setIsNewCategoryOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewCategoryVersionModal
        isOpen={Boolean(newVersionCatId)}
        onClose={() => setNewVersionCatId(null)}
        legalEntityId={legalEntityId}
        categoryId={newVersionCatId ?? ""}
      />

      <NewPolicyModal
        isOpen={isNewPolicyOpen}
        onClose={() => setIsNewPolicyOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewPolicyVersionModal
        isOpen={Boolean(newVersionPolicyId)}
        onClose={() => setNewVersionPolicyId(null)}
        legalEntityId={legalEntityId}
        policyId={newVersionPolicyId ?? ""}
      />

      <NewRatePolicyModal
        isOpen={isNewRateOpen}
        onClose={() => setIsNewRateOpen(false)}
        legalEntityId={legalEntityId}
      />

      <ImportExpensesModal
        isOpen={isImportOpen}
        onClose={() => setIsImportOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
