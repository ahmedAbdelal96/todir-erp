import React, { useState, type ReactNode } from "react";
import {
  TextField as AriaTextField,
  Input as AriaInput,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  type TextFieldProps as AriaTextFieldProps
} from "react-aria-components";
import clsx from "clsx";

export interface PasswordFieldProps
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
    | "id"
    | "autoComplete"
    | "minLength"
  > {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly placeholder?: string | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly isReadOnly?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly className?: string | undefined;
  readonly value?: string | undefined;
  readonly defaultValue?: string | undefined;
  readonly name?: string | undefined;
  readonly isRequired?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly autoFocus?: boolean | undefined;
  readonly autoComplete?: string | undefined;
  readonly minLength?: number | undefined;
  readonly id?: string | undefined;
  readonly onChange?: ((value: string) => void) | undefined;
  readonly startAdornment?: ReactNode | undefined;
}

function EyeOpenIcon(): React.JSX.Element {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

function EyeSlashIcon(): React.JSX.Element {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M9.88 9.88a3 3 0 1 0 4.24 4.24" />
      <path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68" />
      <path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61" />
      <line x1="2" y1="2" x2="22" y2="22" />
    </svg>
  );
}

export function PasswordField({
  label,
  description,
  errorMessage,
  placeholder = "••••••••••••",
  size = "comfortable",
  isReadOnly = false,
  isLocked = false,
  lockedReason,
  className,
  value,
  defaultValue,
  name,
  isRequired,
  isDisabled,
  autoFocus,
  autoComplete = "current-password",
  minLength,
  id,
  onChange,
  startAdornment,
  ...props
}: PasswordFieldProps): React.JSX.Element {
  const [showPassword, setShowPassword] = useState(false);
  const isEffectivelyReadOnly = isReadOnly || isLocked;

  return (
    <AriaTextField
      {...props}
      {...(value !== undefined ? { value } : {})}
      {...(defaultValue !== undefined ? { defaultValue } : {})}
      {...(name !== undefined ? { name } : {})}
      {...(isRequired !== undefined ? { isRequired } : {})}
      {...(isDisabled !== undefined ? { isDisabled } : {})}
      {...(autoFocus !== undefined ? { autoFocus } : {})}
      {...(onChange !== undefined ? { onChange } : {})}
      type={showPassword ? "text" : "password"}
      isReadOnly={isEffectivelyReadOnly}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-field ui-password-field", `ui-field--${size}`, className)}
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
                fontWeight: "600",
                color: "var(--color-content-primary)"
              }}
            >
              {label}
            </AriaLabel>
            {isRequired && (
              <span aria-hidden="true" style={{ color: "var(--color-status-danger)" }}>
                *
              </span>
            )}
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
          backgroundColor: isEffectivelyReadOnly
            ? "var(--color-surface-subtle)"
            : "var(--color-surface-card)",
          border: `1px solid ${
            errorMessage ? "var(--color-status-danger)" : "var(--color-border-default)"
          }`,
          borderRadius: "var(--radius-md)",
          paddingInlineStart: "0.75rem",
          paddingInlineEnd: "0.375rem",
          height:
            size === "compact"
              ? "var(--density-field-height, 2rem)"
              : "var(--density-field-height, 2.5rem)",
          transition: "border-color 150ms ease, box-shadow 150ms ease",
          boxShadow: errorMessage ? "0 0 0 1px var(--color-status-danger)" : "none"
        }}
      >
        {startAdornment && (
          <div style={{ marginInlineEnd: "0.5rem", color: "var(--color-content-muted)" }}>
            {startAdornment}
          </div>
        )}

        <AriaInput
          id={id}
          placeholder={placeholder}
          autoComplete={autoComplete}
          minLength={minLength}
          style={{
            flex: 1,
            border: "none",
            outline: "none",
            backgroundColor: "transparent",
            color: isEffectivelyReadOnly
              ? "var(--color-content-muted)"
              : "var(--color-content-primary)",
            fontSize: "0.875rem",
            fontFamily: "inherit",
            cursor: isEffectivelyReadOnly ? "not-allowed" : "text",
            width: "100%",
            direction: "ltr",
            textAlign: "left"
          }}
        />

        {!isEffectivelyReadOnly && (
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            aria-label={showPassword ? "إخفاء كلمة المرور" : "إظهار كلمة المرور"}
            title={showPassword ? "إخفاء كلمة المرور" : "إظهار كلمة المرور"}
            style={{
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              background: "none",
              border: "none",
              padding: "0.375rem",
              borderRadius: "var(--radius-sm, 0.25rem)",
              cursor: "pointer",
              color: showPassword
                ? "var(--color-action-primary, #0f766e)"
                : "var(--color-content-muted, #64748b)",
              transition: "color 150ms ease, background-color 150ms ease"
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = "var(--color-surface-subtle, #f1f5f9)";
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = "transparent";
            }}
          >
            {showPassword ? <EyeSlashIcon /> : <EyeOpenIcon />}
          </button>
        )}
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
