import React, { useState, useEffect, type ChangeEvent } from "react";
import { TextField } from "./text-field.tsx";
import { Button } from "./button.tsx";
import clsx from "clsx";

export interface SearchFieldProps {
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  readonly onChange?: ((value: string) => void) | undefined;
  readonly onSearch?: ((value: string) => void) | undefined;
  readonly onClear?: (() => void) | undefined;
  readonly placeholder?: string | undefined;
  readonly debounceMs?: number | undefined;
  readonly isPending?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly ariaLabel?: string | undefined;
  readonly className?: string | undefined;
}

export function SearchField({
  value: controlledValue,
  defaultValue = "",
  onChange,
  onSearch,
  onClear,
  placeholder = "البحث...",
  debounceMs = 300,
  isPending = false,
  isDisabled = false,
  ariaLabel = "حقل البحث",
  className
}: SearchFieldProps): React.JSX.Element {
  const [internalValue, setInternalValue] = useState(defaultValue);
  const isControlled = controlledValue !== undefined;
  const currentValue = isControlled ? controlledValue : internalValue;

  useEffect(() => {
    if (debounceMs <= 0) return;
    const timer = setTimeout(() => {
      onSearch?.(currentValue);
    }, debounceMs);
    return () => clearTimeout(timer);
  }, [currentValue, debounceMs, onSearch]);

  const handleChange = (val: string) => {
    if (!isControlled) {
      setInternalValue(val);
    }
    onChange?.(val);
  };

  const handleClear = () => {
    if (!isControlled) {
      setInternalValue("");
    }
    onChange?.("");
    onClear?.();
    onSearch?.("");
  };

  return (
    <div
      className={clsx("ui-search-field", className)}
      style={{
        position: "relative",
        display: "flex",
        alignItems: "center",
        width: "100%",
        maxWidth: "360px"
      }}
    >
      <span
        style={{
          position: "absolute",
          insetInlineStart: "0.75rem",
          color: "var(--color-content-muted)",
          pointerEvents: "none",
          fontSize: "0.875rem",
          display: "flex",
          alignItems: "center",
          zIndex: 1
        }}
        aria-hidden="true"
      >
        🔍
      </span>

      <div style={{ width: "100%" }}>
        <TextField
          value={currentValue}
          onChange={handleChange}
          placeholder={placeholder}
          isDisabled={isDisabled}
          aria-label={ariaLabel}
          style={{
            paddingInlineStart: "2.25rem",
            paddingInlineEnd: currentValue ? "2rem" : "0.75rem"
          }}
        />
      </div>

      {isPending ? (
        <span
          style={{
            position: "absolute",
            insetInlineEnd: "0.75rem",
            fontSize: "0.75rem",
            color: "var(--color-content-muted)",
            animation: "spin 1s linear infinite"
          }}
          aria-hidden="true"
        >
          ⏳
        </span>
      ) : currentValue && !isDisabled ? (
        <button
          type="button"
          onClick={handleClear}
          aria-label="مسح البحث"
          style={{
            position: "absolute",
            insetInlineEnd: "0.5rem",
            background: "none",
            border: "none",
            padding: "0.25rem",
            cursor: "pointer",
            color: "var(--color-content-muted)",
            fontSize: "0.75rem",
            borderRadius: "50%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center"
          }}
        >
          ✕
        </button>
      ) : null}
    </div>
  );
}
