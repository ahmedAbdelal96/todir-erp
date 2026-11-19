import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCashForecast } from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { CreateForecastVersionModal } from "./create-forecast-version-modal.tsx";
import { CreateForecastItemModal } from "./create-forecast-item-modal.tsx";

interface Props {
  readonly legalEntityId: string;
}

export function CashForecastTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");

  const today = new Date().toISOString().split("T")[0];
  const [asOfDate, setAsOfDate] = useState(today);
  const [horizonDays, setHorizonDays] = useState("30");

  const [isVersionOpen, setIsVersionOpen] = useState(false);
  const [isItemOpen, setIsItemOpen] = useState(false);

  const { data: forecastData, isLoading, error, refetch } = useCashForecast(
    legalEntityId,
    asOfDate || undefined,
    parseInt(horizonDays, 10) || 30
  );

  const items = forecastData?.items ?? [];
  const currencyTotals = React.useMemo(() => {
    return items.reduce<Record<string, { inflows: number; outflows: number; net: number }>>((acc, item) => {
      const c = item.currencyCode || "EGP";
      if (!acc[c]) {
        acc[c] = { inflows: 0, outflows: 0, net: 0 };
      }
      if (item.direction === "Inflow") {
        acc[c].inflows += item.amount;
        acc[c].net += item.amount;
      } else {
        acc[c].outflows += item.amount;
        acc[c].net -= item.amount;
      }
      return acc;
    }, {});
  }, [items]);

  const currencies = Object.keys(currencyTotals);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("forecast.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("forecast.subtitle")}</p>
        </div>
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button variant="secondary" data-testid="new-forecast-version" onClick={() => setIsVersionOpen(true)}>
            {t("forecast.newVersionButton")}
          </Button>
          <Button variant="primary" data-testid="new-forecast-item" onClick={() => setIsItemOpen(true)} isDisabled={!forecastData?.id}>
            {t("forecast.newManualItemButton")}
          </Button>
        </div>
      </div>

      {/* Horizon Filter Surface */}
      <Surface style={{ padding: "1rem" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", alignItems: "flex-end" }}>
          <TextField
            label={t("forecast.asOfDateLabel")}
            type="date"
            value={asOfDate}
            onChange={(val) => setAsOfDate(val)}
          />
          <div>
            <Select
              label={t("forecast.horizonLabel")}
              selectedKey={horizonDays}
              onSelectionChange={(key) => setHorizonDays(key ? String(key) : "30")}
              options={[
                { id: "7", label: t("forecast.days7") },
                { id: "14", label: t("forecast.days14") },
                { id: "30", label: t("forecast.days30") },
                { id: "60", label: t("forecast.days60") },
                { id: "90", label: t("forecast.days90") }
              ]}
            />
          </div>
        </div>
      </Surface>

      {/* KPI Ribbon */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" }}>
        {currencies.length > 0 ? (
          currencies.map((curr) => {
            const data = currencyTotals[curr]!;
            return (
              <Surface key={curr} style={{ padding: "1.25rem" }}>
                <div style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-brand-primary)", marginBottom: "0.5rem" }}>
                  {t("forecast.title")} ({curr})
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.5rem", marginBottom: "0.75rem" }}>
                  <div>
                    <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("forecast.inflowsTotal")}</div>
                    <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "#16a34a" }}>
                      <BankingMoney amount={data.inflows} currency={curr} />
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("forecast.outflowsTotal")}</div>
                    <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-status-danger)" }}>
                      <BankingMoney amount={data.outflows} currency={curr} />
                    </div>
                  </div>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.5rem" }}>
                  <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("forecast.netForecast")}:</span>
                  <span style={{ fontSize: "1.125rem", fontWeight: 700, color: data.net >= 0 ? "#16a34a" : "var(--color-status-danger)" }}>
                    <BankingMoney amount={data.net} currency={curr} />
                  </span>
                </div>
              </Surface>
            );
          })
        ) : (
          <Surface style={{ padding: "1.25rem" }}>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("forecast.netForecast")}</div>
            <div style={{ fontSize: "1.375rem", fontWeight: 700, color: "var(--color-brand-primary)", marginTop: "0.25rem" }}>—</div>
          </Surface>
        )}
      </div>

      {/* Forecast Items Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {error ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>
            <p style={{ color: "var(--color-status-danger)", fontWeight: 600 }}>{t("common.error")}</p>
            <Button variant="secondary" onClick={() => void refetch()} style={{ marginTop: "1rem" }}>
              {t("common.retry")}
            </Button>
          </div>
        ) : isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : items.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>
            <p style={{ color: "var(--color-content-muted)" }}>{t("common.noData")}</p>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("forecast.dueDate")}</TableHead>
                <TableHead>{t("forecast.direction")}</TableHead>
                <TableHead align="end">{t("forecast.amount")}</TableHead>
                <TableHead>{t("forecast.sourceType")}</TableHead>
                <TableHead>{t("forecast.description")}</TableHead>
                <TableHead>{t("forecast.confidence")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell style={{ fontWeight: 600 }}>
                    <BdiText>{formatDate(item.dueDate)}</BdiText>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={item.direction === "Inflow" ? "success" : "neutral"}
                      label={item.direction === "Inflow" ? t("forecast.inflow") : t("forecast.outflow")}
                    />
                  </TableCell>
                  <TableCell align="end" style={{ fontWeight: 600 }}>
                    <BankingMoney amount={item.amount} currency={item.currencyCode} />
                  </TableCell>
                  <TableCell>
                    <BdiText>{item.sourceType}</BdiText>
                  </TableCell>
                  <TableCell>{item.description}</TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={item.confidence === "High" ? "success" : "neutral"}
                      label={<BdiText>{item.confidence}</BdiText>}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Surface>

      {/* Modals */}
      <CreateForecastVersionModal
        legalEntityId={legalEntityId}
        isOpen={isVersionOpen}
        onClose={() => setIsVersionOpen(false)}
        onCreated={(date, horizon) => {
          setAsOfDate(date);
          setHorizonDays(horizon);
        }}
      />

      <CreateForecastItemModal
        legalEntityId={legalEntityId}
        forecastVersionId={forecastData?.id ?? ""}
        isOpen={isItemOpen}
        onClose={() => setIsItemOpen(false)}
      />
    </div>
  );
}
