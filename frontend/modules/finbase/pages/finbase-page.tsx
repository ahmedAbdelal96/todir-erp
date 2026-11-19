import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useQueryTab } from "../../../core/routing/use-query-tab.ts";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import {
  useFinBaseScope,
  useFinBaseCurrencies,
  useEntityCurrencies,
  useExchangeRates,
  useApproveExchangeRateMutation,
  useRateSources,
  useFinancialPolicy,
  useRoundingPolicies,
  usePaymentTerms
} from "../hooks/finbase-queries.ts";
import { EnableCurrencyModal } from "../features/enable-currency-modal.tsx";
import { CreateRateModal } from "../features/create-rate-modal.tsx";
import { ConversionPreviewWidget } from "../features/conversion-preview-widget.tsx";
import { SchedulePreviewWidget } from "../features/schedule-preview-widget.tsx";
import { PaymentTermDrawer } from "../features/payment-term-drawer.tsx";
import { CreatePaymentTermModal } from "../features/create-payment-term-modal.tsx";
import { RateSourceModal } from "../features/rate-source-modal.tsx";
import { FinancialPolicyModal } from "../features/financial-policy-modal.tsx";
import { RoundingExplanationCard } from "../features/rounding-explanation-card.tsx";

export type FinBaseTab =
  | "overview"
  | "currencies"
  | "rates"
  | "sources"
  | "paymentTerms"
  | "policies";

export function FinBasePage(): React.JSX.Element {
  const { t } = useTranslation("finbase");
  const outlet = useOutletContext<WorkspaceOutletContext | undefined>();
  const { legalEntityId, baseCurrencyCode } = useFinBaseScope({ legalEntityId: outlet?.legalEntityId });

  const [activeTab, setActiveTab] = useQueryTab<FinBaseTab>({
    defaultTab: "overview",
    allowedTabs: ["overview", "currencies", "rates", "sources", "paymentTerms", "policies"]
  });
  const [includeArchivedTerms, setIncludeArchivedTerms] = useState(false);
  const [isMobile, setIsMobile] = useState(false);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const mql = window.matchMedia("(max-width: 768px)");
    setIsMobile(mql.matches);
    const handler = (e: MediaQueryListEvent) => setIsMobile(e.matches);
    mql.addEventListener("change", handler);
    return () => mql.removeEventListener("change", handler);
  }, []);

  // Modals state
  const [isEnableCurrencyOpen, setIsEnableCurrencyOpen] = useState(false);
  const [isCreateRateOpen, setIsCreateRateOpen] = useState(false);
  const [isRateSourceOpen, setIsRateSourceOpen] = useState(false);
  const [isPolicyModalOpen, setIsPolicyModalOpen] = useState(false);
  const [isCreateTermOpen, setIsCreateTermOpen] = useState(false);
  const [selectedTermIdForDrawer, setSelectedTermIdForDrawer] = useState<string>();

  // Queries
  const { data: currencies = [], isLoading: isLoadingCurrencies } = useFinBaseCurrencies();
  const { data: entityCurrencies = [], isLoading: isLoadingEntityCurrencies } =
    useEntityCurrencies(legalEntityId);
  const { data: exchangeRates = [], isLoading: isLoadingRates } =
    useExchangeRates(legalEntityId);
  const { data: rateSources = [] } = useRateSources(legalEntityId);
  const { data: policy } = useFinancialPolicy(legalEntityId);
  const { data: roundingPolicies = [] } = useRoundingPolicies();
  const { data: paymentTerms = [], isLoading: isLoadingTerms } =
    usePaymentTerms(includeArchivedTerms);

  const approveRateMutation = useApproveExchangeRateMutation();

  const isMultiCurrencyActive = entityCurrencies.length > 1;

  const currentRoundingPolicy = roundingPolicies.find(
    (rp) => rp.id === policy?.roundingPolicyId
  );

  const tabOptions = [
    { id: "overview", label: t("tabs.overview") },
    { id: "currencies", label: t("tabs.currencies") },
    { id: "rates", label: t("tabs.rates") },
    { id: "sources", label: t("tabs.sources") },
    { id: "paymentTerms", label: t("tabs.paymentTerms") },
    { id: "policies", label: t("tabs.policies") }
  ];

  const handleApproveRate = async (rateId: string) => {
    try {
      await approveRateMutation.mutateAsync({
        entityId: legalEntityId,
        id: rateId
      });
    } catch {
      // Handled by query error state
    }
  };

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "var(--space-6)",
        padding: "var(--space-6)",
        maxWidth: "1400px",
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <PageHeader
        title={t("page.title")}
        description={t("page.description")}
        secondaryActions={
          <div style={{ display: "flex", gap: "var(--space-2)", alignItems: "center", flexWrap: "wrap" }}>
            <Button
              variant="secondary"
              type="button"
              onPress={() => setIsEnableCurrencyOpen(true)}
            >
              {t("header.enableCurrencyAction")}
            </Button>
            <Button
              variant="primary"
              type="button"
              onPress={() => setIsCreateRateOpen(true)}
            >
              {t("header.newRateAction")}
            </Button>
          </div>
        }
      />

      {/* Mode and Base Currency Banner */}
      <Surface>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "var(--space-4)"
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-1)" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
              <span style={{ fontSize: "var(--font-size-sm)", color: "var(--color-content-muted)" }}>
                {t("page.baseCurrencyLabel")}:
              </span>
              <span dir="ltr" style={{ fontSize: "var(--font-size-base)", fontWeight: 700 }}>
                {baseCurrencyCode}
              </span>
              <StatusBadge
                intent="neutral"
                label={t("page.baseCurrencyLockedBadge")}
                size="small"
              />
            </div>
            <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
              {t("page.baseCurrencyLockedExplanation")}
            </p>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)" }}>
            <StatusBadge
              intent={isMultiCurrencyActive ? "success" : "info"}
              label={
                isMultiCurrencyActive
                  ? t("page.multiCurrencyModeBadge")
                  : t("page.singleCurrencyModeBadge")
              }
            />
          </div>
        </div>

        {!isMultiCurrencyActive ? (
          <div style={{ marginTop: "var(--space-3)" }}>
            <Alert variant="info" title={t("page.singleCurrencyNoticeTitle")}>
              {t("page.singleCurrencyNoticeBody")}
            </Alert>
          </div>
        ) : null}
      </Surface>

      {/* Responsive Navigation */}
      {isMobile ? (
        <Select
          label={t("tabs.sectionLabel")}
          selectedKey={activeTab}
          onSelectionChange={(k) => setActiveTab(k as FinBaseTab)}
          options={tabOptions}
        />
      ) : (
        <div
          style={{
            display: "flex",
            gap: "var(--space-2)",
            borderBottom: "1px solid var(--color-border-subtle)",
            paddingBottom: "var(--space-2)",
            overflowX: "auto"
          }}
        >
          {tabOptions.map((opt) => {
            const isActive = activeTab === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                onClick={() => setActiveTab(opt.id as FinBaseTab)}
                style={{
                  background: isActive ? "var(--color-surface-subtle)" : "transparent",
                  border: "none",
                  borderBottom: isActive ? "2px solid var(--color-brand-primary)" : "2px solid transparent",
                  padding: "var(--space-2) var(--space-4)",
                  cursor: "pointer",
                  fontSize: "var(--font-size-sm)",
                  fontWeight: isActive ? 600 : 400,
                  color: isActive ? "var(--color-content-primary)" : "var(--color-content-secondary)",
                  borderRadius: "var(--radius-sm) var(--radius-sm) 0 0",
                  whiteSpace: "nowrap"
                }}
              >
                {opt.label}
              </button>
            );
          })}
        </div>
      )}

      {/* TAB CONTENT */}

      {/* 1. OVERVIEW TAB */}
      {activeTab === "overview" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          {/* Quick Metrics */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
              gap: "var(--space-4)"
            }}
          >
            <Surface>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.baseCurrency")}
              </span>
              <div dir="ltr" style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {baseCurrencyCode}
              </div>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.authoritativeLegalEntity")}
              </span>
            </Surface>

            <Surface>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.enabledCurrencies")}
              </span>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {entityCurrencies.length}
              </div>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {isMultiCurrencyActive
                  ? t("overview.metrics.multiCurrencyStatus")
                  : t("overview.metrics.singleCurrencyStatus")}
              </span>
            </Surface>

            <Surface>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.ratesCount")}
              </span>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {exchangeRates.filter((r) => r.status === "Approved").length}
              </div>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.approvedRatesNote")}
              </span>
            </Surface>

            <Surface>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                {t("overview.metrics.termsCount")}
              </span>
              <div style={{ fontSize: "var(--font-size-2xl)", fontWeight: 700, margin: "var(--space-1) 0" }}>
                {paymentTerms.filter((term) => term.status === "Active").length}
              </div>
              <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                {t("overview.metrics.activeTermsNote")}
              </span>
            </Surface>
          </div>

          {/* Interactive Widgets Grid */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(360px, 1fr))",
              gap: "var(--space-6)"
            }}
          >
            <ConversionPreviewWidget
              entityId={legalEntityId}
              baseCurrencyCode={baseCurrencyCode}
            />
            <SchedulePreviewWidget />
          </div>
        </div>
      )}

      {/* 2. CURRENCIES TAB */}
      {activeTab === "currencies" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          <Surface>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "var(--space-3)",
                marginBottom: "var(--space-4)"
              }}
            >
              <div>
                <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                  {t("currencies.entityTable.title")}
                </h3>
                <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                  {t("currencies.entityTable.description")}
                </p>
              </div>
              <Button
                variant="primary"
                type="button"
                onPress={() => setIsEnableCurrencyOpen(true)}
              >
                {t("currencies.entityTable.enableAction")}
              </Button>
            </div>

            {isLoadingEntityCurrencies ? (
              <p style={{ color: "var(--color-content-muted)" }}>{t("page.loading")}</p>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
                {entityCurrencies.map((c) => {
                  const isBase = c.currencyCode.toUpperCase() === baseCurrencyCode.toUpperCase();
                  return (
                    <div
                      key={c.currencyCode}
                      style={{
                        padding: "var(--space-3)",
                        border: "1px solid var(--color-border-subtle)",
                        borderRadius: "var(--radius-md)",
                        display: "flex",
                        flexDirection: "column",
                        gap: "var(--space-2)",
                        backgroundColor: "var(--color-surface-subtle)"
                      }}
                    >
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                        <span dir="ltr" style={{ fontWeight: 700, fontSize: "var(--font-size-base)" }}>{c.currencyCode}</span>
                        {isBase ? (
                          <StatusBadge intent="info" label={t("page.baseCurrencyBadge")} size="small" />
                        ) : (
                          <StatusBadge intent="neutral" label={t("page.foreignCurrencyBadge")} size="small" />
                        )}
                      </div>
                      <div style={{ display: "flex", flexWrap: "wrap", gap: "var(--space-2)", fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                        <span>{t("currencies.entityTable.sales")}: {c.allowSales ? t("common.yes") : t("common.no")}</span>
                        <span>•</span>
                        <span>{t("currencies.entityTable.purchase")}: {c.allowPurchase ? t("common.yes") : t("common.no")}</span>
                        <span>•</span>
                        <span>{t("currencies.entityTable.payment")}: {c.allowPayment ? t("common.yes") : t("common.no")}</span>
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("currencies.entityTable.code")}</TableHead>
                    <TableHead>{t("currencies.entityTable.type")}</TableHead>
                    <TableHead>{t("currencies.entityTable.sales")}</TableHead>
                    <TableHead>{t("currencies.entityTable.purchase")}</TableHead>
                    <TableHead>{t("currencies.entityTable.payment")}</TableHead>
                    <TableHead>{t("currencies.entityTable.manualRate")}</TableHead>
                    <TableHead>{t("currencies.entityTable.status")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entityCurrencies.map((c) => {
                    const isBase = c.currencyCode.toUpperCase() === baseCurrencyCode.toUpperCase();
                    return (
                      <TableRow key={c.currencyCode}>
                        <TableCell>
                          <span dir="ltr" style={{ fontWeight: 600 }}>{c.currencyCode}</span>
                        </TableCell>
                        <TableCell>
                          {isBase ? (
                            <StatusBadge intent="info" label={t("page.baseCurrencyBadge")} size="small" />
                          ) : (
                            <StatusBadge intent="neutral" label={t("page.foreignCurrencyBadge")} size="small" />
                          )}
                        </TableCell>
                        <TableCell>
                          {c.allowSales ? t("common.yes") : t("common.no")}
                        </TableCell>
                        <TableCell>
                          {c.allowPurchase ? t("common.yes") : t("common.no")}
                        </TableCell>
                        <TableCell>
                          {c.allowPayment ? t("common.yes") : t("common.no")}
                        </TableCell>
                        <TableCell>
                          {c.allowManualRate ? t("common.yes") : t("common.no")}
                        </TableCell>
                        <TableCell>
                          <StatusBadge intent="success" label={t("currencies.status.active")} size="small" />
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </Surface>

          {/* Reference ISO Currencies Directory */}
          <Surface>
            <div style={{ marginBottom: "var(--space-3)" }}>
              <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                {t("currencies.referenceDirectory.title")}
              </h3>
              <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                {t("currencies.referenceDirectory.description")}
              </p>
            </div>

            {isLoadingCurrencies ? (
              <p style={{ color: "var(--color-content-muted)" }}>{t("page.loading")}</p>
            ) : (
              <div style={{ display: "flex", gap: "var(--space-2)", flexWrap: "wrap" }}>
                {currencies.map((c) => (
                  <span
                    key={c.code}
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "var(--space-1)",
                      padding: "var(--space-1) var(--space-3)",
                      borderRadius: "var(--radius-full)",
                      backgroundColor: "var(--color-surface-subtle)",
                      border: "1px solid var(--color-border-subtle)",
                      fontSize: "var(--font-size-xs)"
                    }}
                  >
                    <strong dir="ltr">{c.code}</strong>
                    <span>({c.numericCode})</span>
                    <span>·</span>
                    <span>{c.nameAr}</span>
                    <span style={{ color: "var(--color-content-muted)" }}>
                      ({t("currencies.referenceDirectory.minorUnits", { count: c.minorUnit })})
                    </span>
                  </span>
                ))}
              </div>
            )}
          </Surface>
        </div>
      )}

      {/* 3. RATES TAB */}
      {activeTab === "rates" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          <Surface>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "var(--space-3)",
                marginBottom: "var(--space-4)"
              }}
            >
              <div>
                <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                  {t("rates.table.title")}
                </h3>
                <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                  {t("rates.table.description")}
                </p>
              </div>
              <Button
                variant="primary"
                type="button"
                onPress={() => setIsCreateRateOpen(true)}
              >
                {t("rates.table.newRateAction")}
              </Button>
            </div>

            {isLoadingRates ? (
              <p style={{ color: "var(--color-content-muted)" }}>{t("page.loading")}</p>
            ) : exchangeRates.length === 0 ? (
              <Alert variant="info" title={t("rates.table.emptyTitle")}>
                {t("rates.table.emptyDescription")}
              </Alert>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
                {exchangeRates.map((rate) => (
                  <div
                    key={rate.id}
                    style={{
                      padding: "var(--space-3)",
                      border: "1px solid var(--color-border-subtle)",
                      borderRadius: "var(--radius-md)",
                      display: "flex",
                      flexDirection: "column",
                      gap: "var(--space-2)",
                      backgroundColor: "var(--color-surface-subtle)"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "var(--space-2)" }}>
                      <span dir="ltr" style={{ fontWeight: 700, fontSize: "var(--font-size-base)" }}>
                        {t("rates.rateEquation", {
                          from: rate.fromCurrency,
                          rate: rate.rate,
                          to: rate.toCurrency
                        })}
                      </span>
                      <StatusBadge
                        intent={
                          rate.status === "Approved"
                            ? "success"
                            : rate.status === "Draft"
                            ? "warning"
                            : rate.status === "Superseded"
                            ? "neutral"
                            : "danger"
                        }
                        label={t(`rates.status.${rate.status}`)}
                        size="small"
                      />
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                      <span>{t("rates.table.purposeHeader")}: {t(`rates.purposes.${rate.purpose}`)}</span>
                      <span dir="ltr">{rate.effectiveDate}</span>
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                      <span>{t(`rates.sourceTypes.${rate.sourceType}`, rate.sourceType)} {rate.providerReference ? `(${rate.providerReference})` : ""}</span>
                      {rate.status === "Approved" && (
                        <span>{t("rates.table.immutableLabel")}</span>
                      )}
                    </div>

                    {rate.status === "Draft" && (
                      <div style={{ marginTop: "var(--space-1)" }}>
                        <Button
                          variant="primary"
                          type="button"
                          onPress={() => void handleApproveRate(rate.id)}
                          isDisabled={approveRateMutation.isPending}
                        >
                          {t("rates.table.approveAction")}
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("rates.table.rateDirectionHeader")}</TableHead>
                    <TableHead>{t("rates.table.purposeHeader")}</TableHead>
                    <TableHead>{t("rates.table.effectiveDateHeader")}</TableHead>
                    <TableHead>{t("rates.table.sourceHeader")}</TableHead>
                    <TableHead>{t("rates.table.referenceHeader")}</TableHead>
                    <TableHead>{t("rates.table.statusHeader")}</TableHead>
                    <TableHead align="end">{t("rates.table.actionsHeader")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {exchangeRates.map((rate) => (
                    <TableRow key={rate.id}>
                      <TableCell>
                        <span dir="ltr" style={{ fontWeight: 700, fontSize: "var(--font-size-sm)" }}>
                          {t("rates.rateEquation", {
                            from: rate.fromCurrency,
                            rate: rate.rate,
                            to: rate.toCurrency
                          })}
                        </span>
                      </TableCell>
                      <TableCell>
                        {t(`rates.purposes.${rate.purpose}`)}
                      </TableCell>
                      <TableCell>
                        <span dir="ltr">{rate.effectiveDate}</span>
                      </TableCell>
                      <TableCell>{t(`rates.sourceTypes.${rate.sourceType}`, rate.sourceType)}</TableCell>
                      <TableCell>
                        <span dir="ltr" style={{ color: "var(--color-content-muted)", fontSize: "var(--font-size-xs)" }}>
                          {rate.providerReference || "—"}
                        </span>
                      </TableCell>
                      <TableCell>
                        <StatusBadge
                          intent={
                            rate.status === "Approved"
                              ? "success"
                              : rate.status === "Draft"
                              ? "warning"
                              : rate.status === "Superseded"
                              ? "neutral"
                              : "danger"
                          }
                          label={t(`rates.status.${rate.status}`)}
                          size="small"
                        />
                      </TableCell>
                      <TableCell align="end">
                        {rate.status === "Draft" ? (
                          <Button
                            variant="primary"
                            type="button"
                            onPress={() => void handleApproveRate(rate.id)}
                            isDisabled={approveRateMutation.isPending}
                          >
                            {t("rates.table.approveAction")}
                          </Button>
                        ) : (
                          <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                            {t("rates.table.immutableLabel")}
                          </span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Surface>

          {/* Interactive Conversion preview */}
          <ConversionPreviewWidget
            entityId={legalEntityId}
            baseCurrencyCode={baseCurrencyCode}
          />
        </div>
      )}

      {/* 4. RATE SOURCES TAB */}
      {activeTab === "sources" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          <Surface>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "var(--space-3)",
                marginBottom: "var(--space-4)"
              }}
            >
              <div>
                <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                  {t("rateSources.table.title")}
                </h3>
                <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                  {t("rateSources.table.description")}
                </p>
              </div>
              <Button
                variant="primary"
                type="button"
                onPress={() => setIsRateSourceOpen(true)}
              >
                {t("rateSources.table.newSourceAction")}
              </Button>
            </div>

            {rateSources.length === 0 ? (
              <Alert variant="info" title={t("rateSources.table.emptyTitle")}>
                {t("rateSources.table.emptyDescription")}
              </Alert>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
                {rateSources.map((src) => (
                  <div
                    key={src.id}
                    style={{
                      padding: "var(--space-3)",
                      border: "1px solid var(--color-border-subtle)",
                      borderRadius: "var(--radius-md)",
                      display: "flex",
                      flexDirection: "column",
                      gap: "var(--space-2)",
                      backgroundColor: "var(--color-surface-subtle)"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span dir="ltr" style={{ fontWeight: 700 }}>{src.sourceCode}</span>
                      <StatusBadge
                        intent={src.enabled ? "success" : "neutral"}
                        label={src.enabled ? t("rateSources.status.enabled") : t("rateSources.status.disabled")}
                        size="small"
                      />
                    </div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                      <span dir="ltr">
                        {t("rateSources.table.pairValue", {
                          from: src.fromCurrency,
                          to: src.toCurrency
                        })}
                      </span>
                      <span>{t(`rates.purposes.${src.purpose}`)}</span>
                    </div>
                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                      <span>{t("rateSources.table.priorityHeader")}: {src.priority}</span>
                      <span dir="ltr">{t("rateSources.table.secondsValue", { count: src.syncIntervalSeconds })}</span>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("rateSources.table.sourceHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.pairHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.purposeHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.priorityHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.intervalHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.credentialHeader")}</TableHead>
                    <TableHead>{t("rateSources.table.statusHeader")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rateSources.map((src) => (
                    <TableRow key={src.id}>
                      <TableCell>
                        <span dir="ltr" style={{ fontWeight: 600 }}>{src.sourceCode}</span>
                      </TableCell>
                      <TableCell>
                        <span dir="ltr">
                          {t("rateSources.table.pairValue", {
                            from: src.fromCurrency,
                            to: src.toCurrency
                          })}
                        </span>
                      </TableCell>
                      <TableCell>{t(`rates.purposes.${src.purpose}`)}</TableCell>
                      <TableCell><span dir="ltr">{src.priority}</span></TableCell>
                      <TableCell>
                        <span dir="ltr">{t("rateSources.table.secondsValue", { count: src.syncIntervalSeconds })}</span>
                      </TableCell>
                      <TableCell>
                        {src.credentialReference ? (
                          <StatusBadge
                            intent="info"
                            label={t("rateSources.credentialConfigured")}
                            size="small"
                          />
                        ) : (
                          <span style={{ color: "var(--color-content-muted)", fontSize: "var(--font-size-xs)" }}>
                            {t("rateSources.credentialNotRequired")}
                          </span>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge
                          intent={src.enabled ? "success" : "neutral"}
                          label={src.enabled ? t("rateSources.status.enabled") : t("rateSources.status.disabled")}
                          size="small"
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Surface>
        </div>
      )}

      {/* 5. PAYMENT TERMS TAB */}
      {activeTab === "paymentTerms" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          <Surface>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "var(--space-3)",
                marginBottom: "var(--space-4)"
              }}
            >
              <div>
                <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                  {t("paymentTerms.table.title")}
                </h3>
                <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                  {t("paymentTerms.table.description")}
                </p>
              </div>

              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", flexWrap: "wrap" }}>
                <Checkbox
                  isSelected={includeArchivedTerms}
                  onChange={setIncludeArchivedTerms}
                >
                  {t("paymentTerms.table.includeArchived")}
                </Checkbox>
                <Button
                  variant="primary"
                  type="button"
                  onPress={() => setIsCreateTermOpen(true)}
                >
                  {t("paymentTerms.table.createAction")}
                </Button>
              </div>
            </div>

            {isLoadingTerms ? (
              <p style={{ color: "var(--color-content-muted)" }}>{t("page.loading")}</p>
            ) : isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
                {paymentTerms.map((term) => (
                  <div
                    key={term.id}
                    style={{
                      padding: "var(--space-3)",
                      border: "1px solid var(--color-border-subtle)",
                      borderRadius: "var(--radius-md)",
                      display: "flex",
                      flexDirection: "column",
                      gap: "var(--space-2)",
                      backgroundColor: "var(--color-surface-subtle)"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "var(--space-2)" }}>
                      <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                        <span dir="ltr" style={{ fontWeight: 700, fontSize: "var(--font-size-base)" }}>{term.code}</span>
                        <span style={{ fontSize: "var(--font-size-sm)", color: "var(--color-content-secondary)" }}>{term.displayNameAr}</span>
                      </div>
                      <StatusBadge
                        intent={term.status === "Active" ? "success" : "neutral"}
                        label={
                          term.status === "Active"
                            ? t("paymentTerms.status.Active")
                            : t("paymentTerms.status.Archived")
                        }
                        size="small"
                      />
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                      <span dir="ltr">
                        {t("paymentTerms.table.versionPrefix", {
                          number: term.currentVersionNumber ?? 1
                        })}
                      </span>
                      <span dir="ltr">{term.currentEffectiveFrom ?? "—"}</span>
                    </div>

                    <div style={{ marginTop: "var(--space-1)" }}>
                      <Button
                        variant="secondary"
                        type="button"
                        onPress={() => setSelectedTermIdForDrawer(term.id)}
                      >
                        {t("paymentTerms.table.viewHistoryAction")}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("paymentTerms.table.code")}</TableHead>
                    <TableHead>{t("paymentTerms.table.nameAr")}</TableHead>
                    <TableHead>{t("paymentTerms.table.nameEn")}</TableHead>
                    <TableHead>{t("paymentTerms.table.status")}</TableHead>
                    <TableHead>{t("paymentTerms.table.version")}</TableHead>
                    <TableHead>{t("paymentTerms.table.effectiveFrom")}</TableHead>
                    <TableHead align="end">{t("paymentTerms.table.actions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {paymentTerms.map((term) => (
                    <TableRow key={term.id}>
                      <TableCell>
                        <span dir="ltr" style={{ fontWeight: 600 }}>{term.code}</span>
                      </TableCell>
                      <TableCell>{term.displayNameAr}</TableCell>
                      <TableCell>{term.displayNameEn}</TableCell>
                      <TableCell>
                        <StatusBadge
                          intent={term.status === "Active" ? "success" : "neutral"}
                          label={
                            term.status === "Active"
                              ? t("paymentTerms.status.Active")
                              : t("paymentTerms.status.Archived")
                          }
                          size="small"
                        />
                      </TableCell>
                      <TableCell>
                        <span dir="ltr">
                          {t("paymentTerms.table.versionPrefix", {
                            number: term.currentVersionNumber ?? 1
                          })}
                        </span>
                      </TableCell>
                      <TableCell>
                        <span dir="ltr">{term.currentEffectiveFrom ?? "—"}</span>
                      </TableCell>
                      <TableCell align="end">
                        <Button
                          variant="secondary"
                          type="button"
                          onPress={() => setSelectedTermIdForDrawer(term.id)}
                        >
                          {t("paymentTerms.table.viewHistoryAction")}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Surface>

          {/* Live Schedule Calculator */}
          <SchedulePreviewWidget />
        </div>
      )}

      {/* 6. POLICIES TAB */}
      {activeTab === "policies" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)" }}>
          {/* Entity Policy Card */}
          <Surface>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                flexWrap: "wrap",
                gap: "var(--space-3)",
                marginBottom: "var(--space-4)"
              }}
            >
              <div>
                <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
                  {t("policies.entityPolicy.title")}
                </h3>
                <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
                  {t("policies.entityPolicy.description")}
                </p>
              </div>
              <Button
                variant="primary"
                type="button"
                onPress={() => setIsPolicyModalOpen(true)}
              >
                {t("policies.entityPolicy.editAction")}
              </Button>
            </div>

            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                gap: "var(--space-4)"
              }}
            >
              <div
                style={{
                  padding: "var(--space-3)",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid var(--color-border-subtle)",
                  backgroundColor: "var(--color-surface-subtle)"
                }}
              >
                <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.entityPolicy.activeRoundingPolicy")}
                </span>
                <div style={{ fontSize: "var(--font-size-base)", fontWeight: 600, marginTop: "var(--space-1)" }}>
                  {currentRoundingPolicy
                    ? `${currentRoundingPolicy.nameAr} (${currentRoundingPolicy.code})`
                    : "—"}
                </div>
              </div>

              <div
                style={{
                  padding: "var(--space-3)",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid var(--color-border-subtle)",
                  backgroundColor: "var(--color-surface-subtle)"
                }}
              >
                <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.entityPolicy.maxRateAge")}
                </span>
                <div style={{ fontSize: "var(--font-size-base)", fontWeight: 600, marginTop: "var(--space-1)" }}>
                  {policy
                    ? t("policies.entityPolicy.daysCount", { count: policy.exchangeRateMaxAgeDays })
                    : "—"}
                </div>
              </div>

              <div
                style={{
                  padding: "var(--space-3)",
                  borderRadius: "var(--radius-md)",
                  border: "1px solid var(--color-border-subtle)",
                  backgroundColor: "var(--color-surface-subtle)"
                }}
              >
                <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                  {t("policies.entityPolicy.manualRateOverride")}
                </span>
                <div style={{ marginTop: "var(--space-1)" }}>
                  <StatusBadge
                    intent={policy?.manualRateOverrideAllowed ? "warning" : "success"}
                    label={
                      policy?.manualRateOverrideAllowed
                        ? t("policies.entityPolicy.overrideAllowed")
                        : t("policies.entityPolicy.overrideBlocked")
                    }
                  />
                </div>
              </div>
            </div>
          </Surface>

          {/* Rounding Explanation Card */}
          <RoundingExplanationCard policies={roundingPolicies} />
        </div>
      )}

      {/* MODALS & DRAWERS */}
      <EnableCurrencyModal
        isOpen={isEnableCurrencyOpen}
        onClose={() => setIsEnableCurrencyOpen(false)}
        entityId={legalEntityId}
      />

      <CreateRateModal
        isOpen={isCreateRateOpen}
        onClose={() => setIsCreateRateOpen(false)}
        entityId={legalEntityId}
        baseCurrencyCode={baseCurrencyCode}
      />

      <RateSourceModal
        isOpen={isRateSourceOpen}
        onClose={() => setIsRateSourceOpen(false)}
        entityId={legalEntityId}
        baseCurrencyCode={baseCurrencyCode}
      />

      <FinancialPolicyModal
        isOpen={isPolicyModalOpen}
        onClose={() => setIsPolicyModalOpen(false)}
        entityId={legalEntityId}
      />

      <CreatePaymentTermModal
        isOpen={isCreateTermOpen}
        onClose={() => setIsCreateTermOpen(false)}
      />

      <PaymentTermDrawer
        isOpen={Boolean(selectedTermIdForDrawer)}
        onClose={() => setSelectedTermIdForDrawer(undefined)}
        termId={selectedTermIdForDrawer ?? undefined}
      />
    </div>
  );
}
