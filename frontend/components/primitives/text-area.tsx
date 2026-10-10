import React from "react";
import {
  TextField as AriaTextField,
  TextArea as AriaTextArea,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  type TextFieldProps as AriaTextFieldProps
} from "react-aria-components";
import clsx from "clsx";

export interface TextAreaProps extends Omit<AriaTextFieldProps, "className" | "isReadOnly" | "isInvalid"> {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly placeholder?: string;
  readonly rows?: number;
  readonly isReadOnly?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly className?: string;
}

export function TextArea({
  label,
  description,
  errorMessage,
  placeholder,
  rows = 3,
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  className,
  ...props
}: TextAreaProps): React.JSX.Element {
  const isEffectivelyReadOnly = isReadOnly || isLocked;

  return (
    <AriaTextField
      {...props}
      isReadOnly={isEffectivelyReadOnly}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-textarea-field", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.25rem",
        width: "100%",
        fontFamily: "var(--font-sans)"
      }}
    >
      {label && (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <AriaLabel
            style={{
              fontSize: "0.875rem",
              fontWeight: "500",
              color: "var(--color-content-primary)"
            }}
          >
            {label}
            {props.isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
          </AriaLabel>
          {isLocked && lockedReason && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {lockedReason}
            </span>
          )}
        </div>
      )}

      <AriaTextArea
        rows={rows}
        {...(placeholder !== undefined ? { placeholder } : {})}
        style={{
          backgroundColor: isEffectivelyReadOnly ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
          border: `1px solid ${errorMessage ? "var(--color-status-danger)" : "var(--color-border-default)"}`,
          borderRadius: "var(--radius-md)",
          padding: "0.5rem 0.75rem",
          color: isEffectivelyReadOnly ? "var(--color-content-muted)" : "var(--color-content-primary)",
          fontSize: "0.875rem",
          fontFamily: "inherit",
          cursor: isEffectivelyReadOnly ? "not-allowed" : "text",
          width: "100%",
          resize: "vertical",
          outline: "none",
          boxShadow: errorMessage ? "0 0 0 1px var(--color-status-danger)" : "none"
        }}
      />

      {description && !errorMessage && (
        <AriaText slot="description" style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
          {description}
        </AriaText>
      )}

      {errorMessage && (
        <AriaFieldError style={{ fontSize: "0.75rem", color: "var(--color-status-danger)" }}>
          {errorMessage}
        </AriaFieldError>
      )}
    </AriaTextField>
  );
}
