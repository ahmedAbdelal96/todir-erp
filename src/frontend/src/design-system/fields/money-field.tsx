import React, { useState, useEffect, type ChangeEvent } from "react";
import type { MoneyValue } from "../contracts/domain-values.ts";
import { normalizeDecimalInput, isValidDecimal } from "../formatting/decimal-parser.ts";
import { getCurrencyPrecision } from "../contracts/precision-policy.ts";
import { TextField } from "../primitives/text-field.tsx";

export interface MoneyFieldProps {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly value?: MoneyValue;
  readonly defaultValue?: MoneyValue;
  readonly defaultCurrency?: string;
  readonly onChange?: (value: MoneyValue) => void;
  readonly size?: "comfortable" | "compact";
  readonly isReadOnly?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly isRequired?: boolean;
  readonly className?: string;
  readonly name?: string;
}

export function MoneyField({
  label,
  description,
  errorMessage,
  value,
  defaultValue,
  defaultCurrency = "EGP",
  onChange,
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  isRequired = false,
  className,
  name
}: MoneyFieldProps): React.JSX.Element {
  const [internalAmount, setInternalAmount] = useState<string>(
    value?.amount ?? defaultValue?.amount ?? ""
  );
  const currency = value?.currency ?? defaultValue?.currency ?? defaultCurrency;

  useEffect(() => {
    if (value !== undefined && value.amount !== internalAmount) {
      setInternalAmount(value.amount);
    }
  }, [value?.amount]);

  const handleInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    const rawVal = e.target.value;
    const normalized = normalizeDecimalInput(rawVal);
    setInternalAmount(normalized);

    if (onChange) {
      onChange({
        amount: normalized,
        currency
      });
    }
  };

  const precision = getCurrencyPrecision(currency);

  return (
    <div className={className} style={{ width: "100%" }}>
      <TextField
        label={label}
        description={description}
        errorMessage={errorMessage}
        size={size}
        isReadOnly={isReadOnly}
        isLocked={isLocked}
        lockedReason={lockedReason}
        isRequired={isRequired}
        name={name}
        value={internalAmount}
        onChange={(val) => {
          const normalized = normalizeDecimalInput(val);
          setInternalAmount(normalized);
          if (onChange) {
            onChange({ amount: normalized, currency });
          }
        }}
        placeholder={`0.${"0".repeat(precision.standardFractionDigits)}`}
        endAdornment={
          <span
            style={{
              fontWeight: 600,
              fontSize: "0.8125rem",
              color: "var(--color-content-secondary)",
              paddingInline: "0.25rem"
            }}
          >
            {currency}
          </span>
        }
      />
    </div>
  );
}
