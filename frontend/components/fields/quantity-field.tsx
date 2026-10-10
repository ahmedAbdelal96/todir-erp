import React, { useState, useEffect } from "react";
import type { QuantityValue } from "../contracts/domain-values.ts";
import { normalizeDecimalInput } from "../formatting/decimal-parser.ts";
import { DEFAULT_QUANTITY_PRECISION } from "../contracts/precision-policy.ts";
import { TextField } from "../primitives/text-field.tsx";

export interface QuantityFieldProps {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly value?: QuantityValue;
  readonly defaultValue?: QuantityValue;
  readonly defaultUnit?: string;
  readonly onChange?: (value: QuantityValue) => void;
  readonly size?: "comfortable" | "compact";
  readonly isReadOnly?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly isRequired?: boolean;
  readonly className?: string;
  readonly name?: string;
}

export function QuantityField({
  label,
  description,
  errorMessage,
  value,
  defaultValue,
  defaultUnit = "PCS",
  onChange,
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  isRequired = false,
  className,
  name
}: QuantityFieldProps): React.JSX.Element {
  const [internalValue, setInternalValue] = useState<string>(
    value?.value ?? defaultValue?.value ?? ""
  );
  const unit = value?.unitOfMeasure ?? defaultValue?.unitOfMeasure ?? defaultUnit;

  useEffect(() => {
    if (value !== undefined && value.value !== internalValue) {
      setInternalValue(value.value);
    }
  }, [value?.value]);

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
        value={internalValue}
        onChange={(val) => {
          const normalized = normalizeDecimalInput(val);
          setInternalValue(normalized);
          if (onChange) {
            onChange({ value: normalized, unitOfMeasure: unit });
          }
        }}
        placeholder="0.00"
        endAdornment={
          <span
            style={{
              fontWeight: 600,
              fontSize: "0.8125rem",
              color: "var(--color-content-secondary)",
              paddingInline: "0.25rem"
            }}
          >
            {unit}
          </span>
        }
      />
    </div>
  );
}
