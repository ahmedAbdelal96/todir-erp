import React from "react";
import { ComboBox } from "../../../components/primitives/combobox.tsx";
import { useEntityCurrencies } from "../../finbase/public.ts";

export interface PaymentCurrencyPickerProps {
  readonly legalEntityId: string;
  readonly value?: string | undefined;
  readonly onChange: (currencyCode: string) => void;
  readonly label: string;
  readonly isRequired?: boolean | undefined;
}

/** Selects an entity-enabled payment currency; typed text is only a search filter. */
export function PaymentCurrencyPicker({
  legalEntityId,
  value,
  onChange,
  label,
  isRequired = false
}: PaymentCurrencyPickerProps): React.JSX.Element {
  const currenciesQuery = useEntityCurrencies(legalEntityId);
  const currencies = (currenciesQuery.data ?? []).filter((currency) => currency.enabled && currency.allowPayment);

  return (
    <ComboBox
      label={label}
      selectedKey={value ?? null}
      onSelectionChange={(key) => onChange(String(key ?? ""))}
      options={currencies.map((currency) => ({
        id: currency.currencyCode,
        label: currency.currencyCode
      }))}
      isRequired={isRequired}
      isDisabled={currenciesQuery.isError}
    />
  );
}
