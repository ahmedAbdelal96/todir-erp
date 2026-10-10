import React, { type ReactNode } from "react";
import {
  TextField as AriaTextField,
  Input as AriaInput,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  type TextFieldProps as AriaTextFieldProps
} from "react-aria-components";
import clsx from "clsx";

export interface TextFieldProps
  extends Omit<
    AriaTextFieldProps,
    | "className"
    | "isReadOnly"
    | "isInvalid"
    | "value"
    | "defaultValue"
    | "name"
    | "onChange"
    | "type"
    | "isRequired"
    | "autoFocus"
    | "disabled"
    | "isDisabled"
  > {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly placeholder?: string | undefined;
  readonly startAdornment?: ReactNode | undefined;
  readonly endAdornment?: ReactNode | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly className?: string | undefined;
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  readonly name?: string | undefined;
  readonly type?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly autoFocus?: boolean | undefined;
  readonly onChange?: ((value: string) => void) | undefined;
}

export function TextField({
  label,
  description,
  errorMessage,
  placeholder,
  startAdornment,
  endAdornment,
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  className,
  value,
  defaultValue,
  name,
  type,
  isRequired,
  isDisabled,
  autoFocus,
  onChange,
  ...props
}: TextFieldProps): React.JSX.Element {
  const isEffectivelyReadOnly = isReadOnly || isLocked;

  return (
    <AriaTextField
      {...props}
      {...(value !== undefined ? { value } : {})}
      {...(defaultValue !== undefined ? { defaultValue } : {})}
      {...(name !== undefined ? { name } : {})}
      {...(type !== undefined ? { type } : {})}
      {...(isRequired !== undefined ? { isRequired } : {})}
      {...(isDisabled !== undefined ? { isDisabled } : {})}
      {...(autoFocus !== undefined ? { autoFocus } : {})}
      {...(onChange !== undefined ? { onChange } : {})}
      isReadOnly={isEffectivelyReadOnly}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-field", `ui-field--${size}`, className)}
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
          <div style={{ display: "flex", alignItems: "center", gap: "0.25rem" }}>
            <AriaLabel
              style={{
                fontSize: "0.875rem",
                fontWeight: "500",
                color: "var(--color-content-primary)"
              }}
            >
              {label}
            </AriaLabel>
            {isRequired && <span aria-hidden="true" style={{ color: "var(--color-status-danger)" }}>*</span>}
          </div>
          {isLocked && lockedReason && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {lockedReason}
            </span>
          )}
        </div>
      )}

      <div
        style={{
          display: "flex",
          alignItems: "center",
          backgroundColor: isEffectivelyReadOnly ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
          border: `1px solid ${errorMessage ? "var(--color-status-danger)" : "var(--color-border-default)"}`,
          borderRadius: "var(--radius-md)",
          paddingInline: "0.75rem",
          height: size === "compact" ? "var(--density-field-height, 2rem)" : "var(--density-field-height, 2.5rem)",
          transition: "border-color 150ms ease, box-shadow 150ms ease",
          boxShadow: errorMessage ? "0 0 0 1px var(--color-status-danger)" : "none"
        }}
      >
        {startAdornment && <div style={{ marginInlineEnd: "0.5rem", color: "var(--color-content-muted)" }}>{startAdornment}</div>}
        <AriaInput
          {...(placeholder !== undefined ? { placeholder } : {})}
          style={{
            flex: 1,
            border: "none",
            outline: "none",
            backgroundColor: "transparent",
            color: isEffectivelyReadOnly ? "var(--color-content-muted)" : "var(--color-content-primary)",
            fontSize: "0.875rem",
            fontFamily: "inherit",
            cursor: isEffectivelyReadOnly ? "not-allowed" : "text",
            width: "100%"
          }}
        />
        {endAdornment && <div style={{ marginInlineStart: "0.5rem", color: "var(--color-content-muted)" }}>{endAdornment}</div>}
      </div>

      {description && !errorMessage && (
        <AriaText
          slot="description"
          style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}
        >
          {description}
        </AriaText>
      )}

      {errorMessage && (
        <AriaFieldError
          style={{
            fontSize: "0.75rem",
            color: "var(--color-status-danger)",
            display: "block"
          }}
        >
          {errorMessage}
        </AriaFieldError>
      )}
    </AriaTextField>
  );
}
