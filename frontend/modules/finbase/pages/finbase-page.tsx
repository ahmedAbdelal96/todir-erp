import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell,
} from "../../../components/primitives/table.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { finbaseApi } from "../services/finbase-api.ts";
import type { Currency, PaymentScheduleLine } from "../types.ts";

export function FinBasePage(): React.JSX.Element {
  const { t } = useTranslation("finbase");
  const [currencies, setCurrencies] = useState<readonly Currency[]>([]);
  const [money, setMoney] = useState({ amount: "100.00", currency: "EGP" });
  const [schedule, setSchedule] = useState<readonly PaymentScheduleLine[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    void finbaseApi.currencies().then(setCurrencies).catch(() => setError(t("page.currencyLoadError")));
  }, []);

  async function preview() {
    setError(undefined);
    try {
      setSchedule(
        await finbaseApi.previewSchedule(money.amount, new Date().toISOString().slice(0, 10), [
          { sequence: 1, allocationType: "Percentage", percentage: 33.33, dueDateBasis: "InvoiceDate", days: 0, monthOffset: 0 },
          { sequence: 2, allocationType: "Balance", dueDateBasis: "InvoiceDate", days: 30, monthOffset: 0 },
        ])
      );
    } catch {
      setError(t("page.scheduleError"));
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-6)", padding: "var(--space-6)" }}>
      <PageHeader
        title={t("page.title")}
        description={t("page.description")}
      />

      {error ? (
        <ErrorState
          title={t("page.errorTitle")}
          message={error}
          onRetry={() => {
            setError(undefined);
            void finbaseApi.currencies().then(setCurrencies).catch(() => setError(t("page.currencyLoadError")));
          }}
        />
      ) : null}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "var(--space-6)" }}>
        <Surface>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-4)" }}>
            <h2 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>{t("page.currencyReference")}</h2>
            <span style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
              {t("page.activeCurrencyCount", { count: currencies.length })}
            </span>
          </div>
          <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "0 0 var(--space-4) 0" }}>
            {t("page.currencyDescription")}
          </p>
          <div style={{ display: "flex", gap: "var(--space-2)", flexWrap: "wrap" }}>
            {currencies.map((currency) => (
              <span
                key={currency.code}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  padding: "var(--space-1) var(--space-3)",
                  borderRadius: "var(--radius-full)",
                  backgroundColor: "var(--color-surface-subtle)",
                  border: "1px solid var(--color-border-subtle)",
                  fontSize: "var(--font-size-xs)",
                  fontWeight: 500,
                  color: "var(--color-content-primary)",
                }}
              >
                {currency.code} · {currency.nameAr}
              </span>
            ))}
          </div>
        </Surface>

        <Surface>
          <h2 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: "0 0 var(--space-4) 0" }}>{t("page.schedulePreview")}</h2>
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
            <MoneyField label={t("page.documentTotal")} value={money} onChange={setMoney} />
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", fontSize: "var(--font-size-sm)" }}>
              <span style={{ color: "var(--color-content-secondary)" }}>{t("page.displayed")}</span>
              <MoneyDisplay value={money} displayCurrencyCode />
            </div>
            <div>
              <Button variant="primary" type="button" onPress={() => void preview()}>
                {t("page.calculateInstallments")}
              </Button>
            </div>

            {schedule.length > 0 ? (
              <div style={{ marginTop: "var(--space-2)" }}>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t("page.installment")}</TableHead>
                      <TableHead align="end">{t("page.amount")}</TableHead>
                      <TableHead>{t("page.dueDate")}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {schedule.map((line) => (
                      <TableRow key={line.sequence}>
                        <TableCell>{t("page.installmentNumber", { number: line.sequence })}</TableCell>
                        <TableCell align="end">
                          <span dir="ltr" style={{ fontWeight: 600 }}>
                            {line.amount}
                          </span>
                        </TableCell>
                        <TableCell><LocalDateDisplay value={line.dueDate} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            ) : null}
          </div>
        </Surface>
      </div>
    </div>
  );
}
