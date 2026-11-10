import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import {
  useExpenseAdvancesQuery,
  useApproveAdvanceMutation
} from "../../hooks/use-expenses-queries.ts";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import { RequestAdvanceModal } from "../modals/request-advance-modal.tsx";
import { IssueAdvanceModal } from "../modals/issue-advance-modal.tsx";
import {
  ApplyAdvanceModal,
  ReturnAdvanceModal,
  WriteOffAdvanceModal
} from "../modals/apply-advance-modal.tsx";
import type { BusinessAdvanceDto } from "../../types.ts";

export interface ExpensesAdvancesTabProps {
  readonly legalEntityId: string;
}

export function ExpensesAdvancesTab({ legalEntityId }: ExpensesAdvancesTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const isMobile = useIsMobile(768);

  const [stateFilter, setStateFilter] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [isRequestOpen, setIsRequestOpen] = useState(false);
  const [issueAdvance, setIssueAdvance] = useState<BusinessAdvanceDto | null>(null);
  const [applyAdvance, setApplyAdvance] = useState<BusinessAdvanceDto | null>(null);
  const [returnAdvance, setReturnAdvance] = useState<BusinessAdvanceDto | null>(null);
  const [writeOffAdvance, setWriteOffAdvance] = useState<BusinessAdvanceDto | null>(null);

  const advancesQuery = useExpenseAdvancesQuery(legalEntityId, {
    state: stateFilter,
    page,
    pageSize: 15
  });

  const approveMutation = useApproveAdvanceMutation(legalEntityId);

  const handleApprove = async (adv: BusinessAdvanceDto) => {
    await approveMutation.mutateAsync({
      id: adv.id,
      request: { version: adv.version, reason: "Approved by finance manager" }
    });
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("advances.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("advances.description")}
            </p>
          </div>

          <Button variant="primary" onClick={() => setIsRequestOpen(true)}>
            + {t("advances.requestAdvance")}
          </Button>
        </div>

        {/* Filter Pills */}
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          {[
            { id: null, label: t("advances.filterAll") },
            { id: "Requested", label: t("advances.filterRequested") },
            { id: "Approved", label: t("advances.filterApproved") },
            { id: "Issued", label: t("advances.filterIssued") },
            { id: "Settled", label: t("advances.filterSettled") }
          ].map((pill) => (
            <button
              key={String(pill.id)}
              type="button"
              onClick={() => {
                setStateFilter(pill.id);
                setPage(1);
              }}
              style={{
                padding: "0.375rem 0.75rem",
                borderRadius: "var(--radius-full, 9999px)",
                border: `1px solid ${stateFilter === pill.id ? "var(--color-brand-primary, #102a43)" : "var(--color-border-default, #cbd5e1)"}`,
                background: stateFilter === pill.id ? "var(--color-brand-primary, #102a43)" : "#ffffff",
                color: stateFilter === pill.id ? "#ffffff" : "var(--color-content-primary, #0f172a)",
                fontSize: "0.8125rem",
                fontWeight: 600,
                cursor: "pointer"
              }}
            >
              {pill.label}
            </button>
          ))}
        </div>

        {/* Table / Mobile Cards */}
        {advancesQuery.isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
        ) : !advancesQuery.data?.items || advancesQuery.data.items.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("advances.empty")}
          </div>
        ) : (
          <>
            {isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {advancesQuery.data.items.map((adv) => {
                  const outstanding = adv.issuedAmount - adv.appliedAmount - adv.returnedAmount - adv.writtenOffAmount;
                  return (
                    <div
                      key={adv.id}
                      style={{
                        backgroundColor: "#ffffff",
                        border: "1px solid #cbd5e1",
                        borderRadius: "0.5rem",
                        padding: "0.875rem",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.5rem"
                      }}
                    >
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                        <div style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                          {adv.purpose}
                          {adv.isOverdue && (
                            <span
                              style={{
                                marginInlineStart: "0.5rem",
                                padding: "0.125rem 0.375rem",
                                borderRadius: "var(--radius-sm)",
                                backgroundColor: "#fef2f2",
                                color: "#dc2626",
                                fontSize: "0.6875rem",
                                fontWeight: 700
                              }}
                            >
                              {t("advances.overdueWarning")}
                            </span>
                          )}
                        </div>
                        <span
                          style={{
                            padding: "0.25rem 0.625rem",
                            borderRadius: "var(--radius-full, 9999px)",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            backgroundColor: adv.state === "Settled" ? "#ecfdf5" : adv.state === "Issued" ? "#eff6ff" : "#fffbeb",
                            color: adv.state === "Settled" ? "#059669" : adv.state === "Issued" ? "#2563eb" : "#d97706",
                            flexShrink: 0
                          }}
                        >
                          {t(`states.advance.${adv.state}` as any, { defaultValue: adv.state })}
                        </span>
                      </div>

                      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.375rem", fontSize: "0.8125rem" }}>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("common.amount")}: </span>
                          <bdi style={{ fontWeight: 600 }}>{adv.amount.toFixed(2)} {adv.currencyCode}</bdi>
                        </div>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("advances.disbursed")}: </span>
                          <bdi style={{ fontWeight: 600 }}>{adv.issuedAmount.toFixed(2)} {adv.currencyCode}</bdi>
                        </div>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("advances.applied")}: </span>
                          <bdi>{adv.appliedAmount.toFixed(2)} {adv.currencyCode}</bdi>
                        </div>
                        <div>
                          <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("advances.outstanding")}: </span>
                          <bdi style={{ fontWeight: 700, color: outstanding > 0 ? "var(--color-status-warning)" : "inherit" }}>
                            {outstanding.toFixed(2)} {adv.currencyCode}
                          </bdi>
                        </div>
                      </div>

                      <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap", marginTop: "0.25rem", paddingTop: "0.5rem", borderTop: "1px solid #f1f5f9" }}>
                        {adv.state === "Requested" && (
                          <Button
                            variant="secondary"
                            size="compact"
                            onClick={() => handleApprove(adv)}
                            isDisabled={approveMutation.isPending}
                          >
                            {t("review.approveAction")}
                          </Button>
                        )}
                        {adv.state === "Approved" && (
                          <Button variant="primary" size="compact" onClick={() => setIssueAdvance(adv)}>
                            {t("advances.issueAction")}
                          </Button>
                        )}
                        {adv.state === "Issued" && (
                          <>
                            <Button variant="secondary" size="compact" onClick={() => setApplyAdvance(adv)}>
                              {t("advances.applyAction")}
                            </Button>
                            <Button variant="secondary" size="compact" onClick={() => setReturnAdvance(adv)}>
                              {t("advances.returnAction")}
                            </Button>
                            <Button variant="danger" size="compact" onClick={() => setWriteOffAdvance(adv)}>
                              {t("advances.writeOffAction")}
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div style={{ width: "100%", overflowX: "auto" }}>
                <Table style={{ minWidth: "750px" }}>
                  <thead>
                    <tr>
                      <th>{t("common.purpose")}</th>
                      <th>{t("common.amount")}</th>
                      <th>{t("advances.disbursed")}</th>
                      <th>{t("advances.applied")}</th>
                      <th>{t("advances.returned")}</th>
                      <th>{t("advances.outstanding")}</th>
                      <th>{t("common.status")}</th>
                      <th>{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {advancesQuery.data.items.map((adv) => {
                      const outstanding = adv.issuedAmount - adv.appliedAmount - adv.returnedAmount - adv.writtenOffAmount;
                      return (
                        <tr key={adv.id}>
                          <td style={{ fontWeight: 600 }}>
                            {adv.purpose}
                            {adv.isOverdue && (
                              <span
                                style={{
                                  marginInlineStart: "0.5rem",
                                  padding: "0.125rem 0.375rem",
                                  borderRadius: "var(--radius-sm)",
                                  backgroundColor: "#fef2f2",
                                  color: "#dc2626",
                                  fontSize: "0.6875rem",
                                  fontWeight: 700
                                }}
                              >
                                {t("advances.overdueWarning")}
                              </span>
                            )}
                          </td>
                          <td>
                            <bdi>
                              {adv.amount.toFixed(2)} {adv.currencyCode}
                            </bdi>
                          </td>
                          <td>
                            <bdi>
                              {adv.issuedAmount.toFixed(2)} {adv.currencyCode}
                            </bdi>
                          </td>
                          <td>
                            <bdi>
                              {adv.appliedAmount.toFixed(2)} {adv.currencyCode}
                            </bdi>
                          </td>
                          <td>
                            <bdi>
                              {adv.returnedAmount.toFixed(2)} {adv.currencyCode}
                            </bdi>
                          </td>
                          <td style={{ fontWeight: 700, color: outstanding > 0 ? "var(--color-status-warning)" : "inherit" }}>
                            <bdi>
                              {outstanding.toFixed(2)} {adv.currencyCode}
                            </bdi>
                          </td>
                          <td>
                            <span
                              style={{
                                padding: "0.25rem 0.625rem",
                                borderRadius: "var(--radius-full, 9999px)",
                                fontSize: "0.75rem",
                                fontWeight: 600,
                                backgroundColor: adv.state === "Settled" ? "#ecfdf5" : adv.state === "Issued" ? "#eff6ff" : "#fffbeb",
                                color: adv.state === "Settled" ? "#059669" : adv.state === "Issued" ? "#2563eb" : "#d97706"
                              }}
                            >
                              {t(`states.advance.${adv.state}` as any, { defaultValue: adv.state })}
                            </span>
                          </td>
                          <td>
                            <div style={{ display: "flex", gap: "0.375rem", flexWrap: "wrap" }}>
                              {adv.state === "Requested" && (
                                <Button
                                  variant="secondary"
                                  size="compact"
                                  onClick={() => handleApprove(adv)}
                                  isDisabled={approveMutation.isPending}
                                >
                                  {t("review.approveAction")}
                                </Button>
                              )}
                              {adv.state === "Approved" && (
                                <Button variant="primary" size="compact" onClick={() => setIssueAdvance(adv)}>
                                  {t("advances.issueAction")}
                                </Button>
                              )}
                              {adv.state === "Issued" && (
                                <>
                                  <Button variant="secondary" size="compact" onClick={() => setApplyAdvance(adv)}>
                                    {t("advances.applyAction")}
                                  </Button>
                                  <Button variant="secondary" size="compact" onClick={() => setReturnAdvance(adv)}>
                                    {t("advances.returnAction")}
                                  </Button>
                                  <Button variant="danger" size="compact" onClick={() => setWriteOffAdvance(adv)}>
                                    {t("advances.writeOffAction")}
                                  </Button>
                                </>
                              )}
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </Table>
              </div>
            )}

            {advancesQuery.data.totalCount > 15 && (
              <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
                <Pagination
                  page={page}
                  totalPages={Math.ceil(advancesQuery.data.totalCount / 15)}
                  onPageChange={setPage}
                />
              </div>
            )}
          </>
        )}
      </Surface>

      <RequestAdvanceModal
        isOpen={isRequestOpen}
        onClose={() => setIsRequestOpen(false)}
        legalEntityId={legalEntityId}
      />

      <IssueAdvanceModal
        isOpen={Boolean(issueAdvance)}
        onClose={() => setIssueAdvance(null)}
        legalEntityId={legalEntityId}
        advance={issueAdvance}
      />

      <ApplyAdvanceModal
        isOpen={Boolean(applyAdvance)}
        onClose={() => setApplyAdvance(null)}
        legalEntityId={legalEntityId}
        advance={applyAdvance}
      />

      <ReturnAdvanceModal
        isOpen={Boolean(returnAdvance)}
        onClose={() => setReturnAdvance(null)}
        legalEntityId={legalEntityId}
        advance={returnAdvance}
      />

      <WriteOffAdvanceModal
        isOpen={Boolean(writeOffAdvance)}
        onClose={() => setWriteOffAdvance(null)}
        legalEntityId={legalEntityId}
        advance={writeOffAdvance}
      />
    </div>
  );
}
