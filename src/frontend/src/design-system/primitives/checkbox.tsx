import React, { type ReactNode } from "react";
import {
  Checkbox as AriaCheckbox,
  CheckboxGroup as AriaCheckboxGroup,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  type CheckboxProps as AriaCheckboxProps,
  type CheckboxGroupProps as AriaCheckboxGroupProps
} from "react-aria-components";
import clsx from "clsx";

export interface CheckboxProps extends Omit<AriaCheckboxProps, "className" | "isReadOnly" | "isDisabled" | "children"> {
  readonly label?: ReactNode;
  readonly description?: string;
  readonly isReadOnly?: boolean;
  readonly isDisabled?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly className?: string;
  readonly children?: ReactNode;
}

export function Checkbox({
  label,
  description,
  isLocked = false,
  lockedReason,
  className,
  isReadOnly,
  isDisabled,
  children,
  ...props
}: CheckboxProps): React.JSX.Element {
  const isEffectiveDisabled = isDisabled || isLocked;
  const isEffectiveReadOnly = isReadOnly;

  return (
    <AriaCheckbox
      {...props}
      {...(isEffectiveDisabled !== undefined ? { isDisabled: isEffectiveDisabled } : {})}
      {...(isEffectiveReadOnly !== undefined ? { isReadOnly: isEffectiveReadOnly } : {})}
      className={clsx("ui-checkbox", className)}
      style={{
        display: "inline-flex",
        alignItems: "flex-start",
        gap: "0.5rem",
        fontFamily: "var(--font-sans)",
        cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
        opacity: isEffectiveDisabled ? 0.6 : 1,
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        userSelect: "none"
      }}
    >
      {({ isSelected, isIndeterminate, isFocusVisible }) => (
        <>
          <div
            style={{
              width: "1.125rem",
              height: "1.125rem",
              borderRadius: "var(--radius-sm, 0.25rem)",
              border: `1px solid ${isSelected || isIndeterminate ? "var(--color-action-primary)" : "var(--color-border-default)"}`,
              backgroundColor: isSelected || isIndeterminate ? "var(--color-action-primary)" : "var(--color-surface-card)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "var(--color-action-primary-content)",
              outline: isFocusVisible ? "2px solid var(--color-action-primary)" : "none",
              outlineOffset: "2px",
              marginTop: "0.125rem",
              transition: "background-color 150ms ease, border-color 150ms ease"
            }}
          >
            {isIndeterminate ? (
              <svg width="10" height="2" viewBox="0 0 10 2" fill="none">
                <rect width="10" height="2" fill="currentColor" rx="1" />
              </svg>
            ) : isSelected ? (
              <svg width="10" height="8" viewBox="0 0 10 8" fill="none">
                <path
                  d="M1 4L3.5 6.5L9 1"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            ) : null}
          </div>
          <div style={{ display: "flex", flexDirection: "column" }}>
            <span>{label ?? children}</span>
            {description && (
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {description}
              </span>
            )}
            {isLocked && lockedReason && (
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {lockedReason}
              </span>
            )}
          </div>
        </>
      )}
    </AriaCheckbox>
  );
}

export interface CheckboxGroupProps extends Omit<AriaCheckboxGroupProps, "className" | "isInvalid" | "children"> {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly className?: string;
  readonly children?: ReactNode;
}

export function CheckboxGroup({
  label,
  description,
  errorMessage,
  className,
  children,
  ...props
}: CheckboxGroupProps): React.JSX.Element {
  return (
    <AriaCheckboxGroup
      {...props}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-checkbox-group", className)}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "0.5rem",
        fontFamily: "var(--font-sans)"
      }}
    >
      {label && (
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
      )}
      <div style={{ display: "flex", flexDirection: "column", gap: "0.375rem" }}>{children}</div>
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
    </AriaCheckboxGroup>
  );
}
