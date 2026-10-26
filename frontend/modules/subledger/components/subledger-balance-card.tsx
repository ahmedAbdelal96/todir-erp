import React from "react";
import { useTranslation } from "react-i18next";
import { StatCard } from "../../../components/pages/property-grid.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import {
  FileTextIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  ShieldCheckIcon
} from "../../../components/primitives/icons.tsx";
import type { PartyBalanceResult, PartyCurrencyBalance } from "../types.ts";

export interface SubledgerBalanceCardProps {
  readonly balance?: PartyBalanceResult | PartyCurrencyBalance | null | undefined;
  readonly isLoading?: boolean | undefined;
  readonly title?: string | undefined;
  readonly creditLimitAmount?: string | null | undefined;
  readonly creditLimitCurrency?: string | null | undefined;
  readonly availableCreditAmount?: string | null | undefined;
  readonly className?: string | undefined;
}

export function SubledgerBalanceCard({
  balance,
  isLoading = false,
  title,
  creditLimitAmount,
  creditLimitCurrency,
  availableCreditAmount,
  className
}: SubledgerBalanceCardProps): React.JSX.Element {
  const { t } = useTranslation("subledger");

  if (isLoading) {
    return (
      <div className={`p-6 bg-white border border-slate-200 rounded-lg shadow-sm text-center ${className || ""}`}>
        <p className="text-xs text-slate-500">{t("common.loadingExposure")}</p>
      </div>
    );
  }

  // Extract the primary currency balance
  let currencyBalance: PartyCurrencyBalance = {
    currencyCode: "EGP",
    amountDue: "0",
    availableCredits: "0",
    netBalance: "0",
    overdueExposure: "0"
  };

  let effectiveCreditLimit = creditLimitAmount;
  let effectiveCreditCurrency = creditLimitCurrency;
  let effectiveAvailableLimit = availableCreditAmount;

  if (balance) {
    if ("currencies" in balance) {
      // It's a PartyBalanceResult
      if (balance.currencies && balance.currencies.length > 0 && balance.currencies[0]) {
        currencyBalance = balance.currencies[0];
      }
      effectiveCreditLimit = balance.creditLimitAmount ?? creditLimitAmount;
      effectiveCreditCurrency = balance.creditLimitCurrency ?? creditLimitCurrency ?? currencyBalance.currencyCode;
      effectiveAvailableLimit = balance.availableCreditAmount ?? availableCreditAmount;
    } else {
      // It's already a PartyCurrencyBalance
      currencyBalance = balance;
    }
  }

  return (
    <div className={`space-y-2 ${className || ""}`}>
      {title && (
        <h4 className="text-xs font-bold text-slate-700 uppercase tracking-wider">{title}</h4>
      )}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "0.75rem",
          width: "100%"
        }}
      >
        {/* 1. Amount Due */}
        <StatCard
          title={t("partyFinancial.amountDue")}
          value={
            <MoneyDisplay
              value={{ amount: currencyBalance.amountDue, currency: currencyBalance.currencyCode }}
              displayCurrencyCode
              size="large"
            />
          }
          intent="primary"
          icon={<FileTextIcon size={20} />}
        />

        {/* 2. Available Credits */}
        <StatCard
          title={t("partyFinancial.availableCredits")}
          value={
            <MoneyDisplay
              value={{ amount: currencyBalance.availableCredits, currency: currencyBalance.currencyCode }}
              displayCurrencyCode
              size="large"
            />
          }
          intent="info"
          icon={<CheckCircleIcon size={20} />}
        />

        {/* 3. Net Balance */}
        <StatCard
          title={t("partyFinancial.netBalance")}
          value={
            <MoneyDisplay
              value={{ amount: currencyBalance.netBalance, currency: currencyBalance.currencyCode }}
              displayCurrencyCode
              size="large"
            />
          }
          intent={parseFloat(currencyBalance.netBalance) > 0 ? "warning" : "success"}
          icon={<ShieldCheckIcon size={20} />}
        />

        {/* 4. Overdue Amount */}
        <StatCard
          title={t("partyFinancial.overdueAmount")}
          value={
            <MoneyDisplay
              value={{ amount: currencyBalance.overdueExposure, currency: currencyBalance.currencyCode }}
              displayCurrencyCode
              size="large"
            />
          }
          intent={parseFloat(currencyBalance.overdueExposure) > 0 ? "danger" : "neutral"}
          icon={<AlertCircleIcon size={20} />}
        />

        {/* 5. Credit Limit if present */}
        {effectiveCreditLimit && (
          <StatCard
            title={t("partyFinancial.creditLimit")}
            value={
              <MoneyDisplay
                value={{ amount: effectiveCreditLimit, currency: effectiveCreditCurrency || currencyBalance.currencyCode }}
                displayCurrencyCode
                size="large"
              />
            }
            subtitle={
              effectiveAvailableLimit
                ? `${t("partyFinancial.availableLimit")}: ${effectiveAvailableLimit} ${effectiveCreditCurrency || currencyBalance.currencyCode}`
                : undefined
            }
            intent="neutral"
            icon={<ShieldCheckIcon size={20} />}
          />
        )}
      </div>
    </div>
  );
}
