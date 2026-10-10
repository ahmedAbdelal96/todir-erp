import React, { type ReactNode } from "react";
import {
  Radio as AriaRadio,
  RadioGroup as AriaRadioGroup,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  type RadioProps as AriaRadioProps,
  type RadioGroupProps as AriaRadioGroupProps
} from "react-aria-components";
import clsx from "clsx";

export interface RadioProps extends Omit<AriaRadioProps, "className" | "children"> {
  readonly label?: ReactNode;
  readonly description?: string;
  readonly className?: string;
  readonly children?: ReactNode;
}

export function Radio({
  label,
  description,
  className,
  children,
  ...props
}: RadioProps): React.JSX.Element {
  return (
    <AriaRadio
      {...props}
      className={clsx("ui-radio", className)}
      style={{
        display: "inline-flex",
        alignItems: "flex-start",
        gap: "0.5rem",
        fontFamily: "var(--font-sans)",
        cursor: props.isDisabled ? "not-allowed" : "pointer",
        opacity: props.isDisabled ? 0.6 : 1,
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        userSelect: "none"
      }}
    >
      {({ isSelected, isFocusVisible }) => (
        <>
          <div
            style={{
              width: "1.125rem",
              height: "1.125rem",
              borderRadius: "50%",
              border: `1px solid ${isSelected ? "var(--color-action-primary)" : "var(--color-border-default)"}`,
              backgroundColor: "var(--color-surface-card)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              outline: isFocusVisible ? "2px solid var(--color-action-primary)" : "none",
              outlineOffset: "2px",
              marginTop: "0.125rem",
              flexShrink: 0
            }}
          >
            {isSelected && (
              <div
                style={{
                  width: "0.5rem",
                  height: "0.5rem",
                  borderRadius: "50%",
                  backgroundColor: "var(--color-action-primary)"
                }}
              />
            )}
          </div>
          <div style={{ display: "flex", flexDirection: "column" }}>
            <span>{label ?? children}</span>
            {description && (
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                {description}
              </span>
            )}
          </div>
        </>
      )}
    </AriaRadio>
  );
}

export interface RadioGroupProps extends Omit<AriaRadioGroupProps, "className" | "orientation" | "isInvalid" | "children"> {
  readonly label?: string;
  readonly description?: string;
  readonly errorMessage?: string;
  readonly orientation?: "horizontal" | "vertical";
  readonly className?: string;
  readonly children?: ReactNode;
}

export function RadioGroup({
  label,
  description,
  errorMessage,
  orientation = "vertical",
  className,
  children,
  ...props
}: RadioGroupProps): React.JSX.Element {
  return (
    <AriaRadioGroup
      {...props}
      orientation={orientation}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-radio-group", className)}
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
      <div
        style={{
          display: "flex",
          flexDirection: orientation === "horizontal" ? "row" : "column",
          gap: orientation === "horizontal" ? "1.25rem" : "0.5rem",
          flexWrap: orientation === "horizontal" ? "wrap" : "nowrap"
        }}
      >
        {children}
      </div>
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
    </AriaRadioGroup>
  );
}
