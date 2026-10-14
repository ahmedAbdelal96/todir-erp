import React from "react";
import { Alert } from "../primitives/alert.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface FormErrorItem {
  readonly field?: string | undefined;
  readonly message: string;
}

export interface ValidationSummaryProps {
  readonly title?: string | undefined;
  readonly errors: readonly FormErrorItem[] | Record<string, string | undefined>;
  readonly onFieldFocus?: ((fieldName: string) => void) | undefined;
  readonly className?: string | undefined;
}

export function ValidationSummary({
  title,
  errors,
  onFieldFocus,
  className
}: ValidationSummaryProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const summaryTitle = title ?? t("components.validationSummary");
  const normalizedErrors: FormErrorItem[] = Array.isArray(errors)
    ? errors
    : Object.entries(errors)
        .filter(([_, msg]) => Boolean(msg))
        .map(([field, message]) => ({ field, message: message! }));

  if (normalizedErrors.length === 0) {
    return <></>;
  }

  return (
    <div className={clsx("ui-validation-summary", className)}>
      <Alert variant="danger" title={summaryTitle}>
        <ul style={{ margin: "0.25rem 0 0 0", paddingInlineStart: "1.25rem", display: "flex", flexDirection: "column", gap: "0.25rem" }}>
          {normalizedErrors.map((err, idx) => (
            <li key={`err-${idx}`}>
              {err.field && onFieldFocus ? (
                <button
                  type="button"
                  onClick={() => onFieldFocus(err.field!)}
                  style={{
                    background: "none",
                    border: "none",
                    padding: 0,
                    color: "inherit",
                    textDecoration: "underline",
                    cursor: "pointer",
                    font: "inherit",
                    textAlign: "start"
                  }}
                >
                  {err.message}
                </button>
              ) : (
                <span>{err.message}</span>
              )}
            </li>
          ))}
        </ul>
      </Alert>
    </div>
  );
}
