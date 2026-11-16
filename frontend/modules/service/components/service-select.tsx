import React from "react";

export interface ServiceSelectOption {
  readonly value: string;
  readonly label: string;
}

export interface ServiceSelectProps {
  readonly value: string;
  readonly onChange: (val: string) => void;
  readonly options: readonly ServiceSelectOption[];
  readonly disabled?: boolean | undefined;
  readonly style?: React.CSSProperties | undefined;
  readonly "data-testid"?: string | undefined;
}

export function ServiceSelect({
  value,
  onChange,
  options,
  disabled = false,
  style,
  "data-testid": testId
}: ServiceSelectProps): React.JSX.Element {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      data-testid={testId}
      style={{
        width: "100%",
        padding: "0.5rem 0.75rem",
        borderRadius: "var(--radius-md, 0.375rem)",
        border: "1px solid var(--color-border-default, #cbd5e1)",
        backgroundColor: disabled ? "var(--color-surface-subtle, #f8fafc)" : "#ffffff",
        color: "var(--color-content-primary, #0f172a)",
        fontSize: "0.875rem",
        boxSizing: "border-box",
        cursor: disabled ? "not-allowed" : "pointer",
        ...style
      }}
    >
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );
}
