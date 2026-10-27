import React, { useRef } from "react";
import { TextField } from "../primitives/text-field.tsx";
import { LocalDateDisplay } from "./local-date-display.tsx";

export interface LocalDateFieldProps {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  readonly onChange?: ((value: string) => void) | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly minDate?: string | undefined;
  readonly maxDate?: string | undefined;
  readonly className?: string | undefined;
  readonly name?: string | undefined;
  readonly "aria-label"?: string | undefined;
}

export function LocalDateField({
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
  isDisabled = false,
  className,
  name,
  "aria-label": ariaLabel
}: LocalDateFieldProps): React.JSX.Element {
  const pickerRef = useRef<HTMLInputElement>(null);

  const handlePickerChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.value && onChange) {
      onChange(e.target.value);
    }
  };

  const handleOpenPicker = () => {
    if (pickerRef.current && !isReadOnly && !isDisabled && !isLocked) {
      if (typeof pickerRef.current.showPicker === "function") {
        pickerRef.current.showPicker();
      } else {
        pickerRef.current.focus();
        pickerRef.current.click();
      }
    }
  };

  const effectiveValue = value ?? defaultValue ?? "";
  const isValidDate = Boolean(effectiveValue && /^\d{4}-\d{2}-\d{2}$/.test(effectiveValue));
  const computedAriaLabel = ariaLabel || label || undefined;

  return (
    <div className={className} style={{ width: "100%", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
      <div style={{ position: "relative", display: "flex", alignItems: "center", width: "100%" }}>
        <TextField
          type="text"
          label={label}
          description={description}
          errorMessage={errorMessage}
          size={size}
          isReadOnly={isReadOnly}
          isLocked={isLocked}
          lockedReason={lockedReason}
          isRequired={isRequired}
          isDisabled={isDisabled}
          name={name}
          value={effectiveValue}
          onChange={onChange}
          {...(computedAriaLabel ? { "aria-label": computedAriaLabel } : {})}
          placeholder="2026-01-01"
          inputDir="ltr"
          endAdornment={
            <button
              type="button"
              onClick={handleOpenPicker}
              tabIndex={-1}
              aria-hidden="true"
              disabled={isReadOnly || isDisabled || isLocked}
              style={{
                background: "none",
                border: "none",
                cursor: (isReadOnly || isDisabled || isLocked) ? "default" : "pointer",
                padding: "0.25rem",
                display: "flex",
                alignItems: "center",
                color: "#64748b",
                fontSize: "0.875rem"
              }}
            >
              📅
            </button>
          }
        />
        <input
          ref={pickerRef}
          type="date"
          tabIndex={-1}
          aria-hidden="true"
          value={isValidDate ? effectiveValue : ""}
          onChange={handlePickerChange}
          style={{
            position: "absolute",
            width: "1px",
            height: "1px",
            opacity: 0,
            pointerEvents: "none",
            bottom: 0,
            insetInlineEnd: "1rem"
          }}
        />
      </div>
      {isValidDate && !errorMessage && (
        <div style={{ fontSize: "0.75rem", color: "#1e3a8a", fontWeight: 600, paddingInlineStart: "0.25rem" }}>
          <LocalDateDisplay value={effectiveValue} />
        </div>
      )}
    </div>
  );
}
