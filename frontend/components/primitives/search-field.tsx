import React, { useState, useEffect } from "react";
import { TextField } from "./text-field.tsx";
import { SearchIcon, CloseIcon } from "./icons.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

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
  readonly size?: "comfortable" | "compact" | undefined;
}

export function SearchField({
  value: controlledValue,
  defaultValue = "",
  onChange,
  onSearch,
  onClear,
  placeholder,
  debounceMs = 300,
  isPending = false,
  isDisabled = false,
  ariaLabel,
  className,
  size
}: SearchFieldProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const searchPlaceholder = placeholder ?? t("components.searchPlaceholder");
  const searchAriaLabel = ariaLabel ?? t("components.searchField");
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

  const startAdornment = (
    <span
      style={{
        color: "var(--color-content-muted, #94a3b8)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center"
      }}
      aria-hidden="true"
    >
      <SearchIcon size={16} />
    </span>
  );

  const endAdornment = isPending ? (
    <span
      style={{
        width: "1rem",
        height: "1rem",
        border: "2px solid var(--color-border-default)",
        borderTopColor: "var(--color-action-primary)",
        borderRadius: "50%",
        animation: "spin 0.6s linear infinite",
        display: "inline-block"
      }}
      aria-hidden="true"
    />
  ) : currentValue && !isDisabled ? (
    <button
      type="button"
      onClick={handleClear}
      aria-label={t("components.clearSearch")}
      style={{
        background: "none",
        border: "none",
        padding: "0.125rem",
        cursor: "pointer",
        color: "var(--color-content-muted, #94a3b8)",
        borderRadius: "50%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        transition: "color 150ms ease"
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.color = "var(--color-content-primary, #0f172a)";
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.color = "var(--color-content-muted, #94a3b8)";
      }}
    >
      <CloseIcon size={14} />
    </button>
  ) : undefined;

  return (
    <div
      className={clsx("ui-search-field", className)}
      style={{
        width: "100%",
        maxWidth: "360px"
      }}
    >
      <TextField
        size={size}
        value={currentValue}
        onChange={handleChange}
        placeholder={searchPlaceholder}
        isDisabled={isDisabled}
        aria-label={searchAriaLabel}
        startAdornment={startAdornment}
        endAdornment={endAdornment}
      />
    </div>
  );
}
