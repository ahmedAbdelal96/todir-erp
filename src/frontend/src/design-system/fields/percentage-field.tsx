import React from "react";
import { normalizeDecimalInput } from "../formatting/decimal-parser.ts";
import { TextField } from "../primitives/text-field.tsx";

export interface PercentageFieldProps {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly value?: string;
  readonly defaultValue?: string;
  readonly onChange?: (value: string) => void;
  readonly size?: "comfortable" | "compact";
  readonly isReadOnly?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly isRequired?: boolean;
  readonly className?: string;
  readonly name?: string;
}

export function PercentageField({
  label,
  description,
  errorMessage,
  value,
  defaultValue,
  onChange,
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  isRequired = false,
  className,
  name
}: PercentageFieldProps): React.JSX.Element {
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
        value={value}
        defaultValue={defaultValue}
        onChange={(val) => {
          const normalized = normalizeDecimalInput(val);
          if (onChange) {
            onChange(normalized);
          }
        }}
        placeholder="0.00"
        endAdornment={
          <span
            style={{
              fontWeight: 600,
              fontSize: "0.875rem",
              color: "var(--color-content-secondary)",
              paddingInline: "0.25rem"
            }}
          >
            %
          </span>
        }
      />
    </div>
  );
}
