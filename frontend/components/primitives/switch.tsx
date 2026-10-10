import React, { type ReactNode } from "react";
import {
  Switch as AriaSwitch,
  type SwitchProps as AriaSwitchProps
} from "react-aria-components";
import clsx from "clsx";

export interface SwitchProps extends Omit<AriaSwitchProps, "className" | "isReadOnly" | "isDisabled" | "children"> {
  readonly label?: ReactNode;
  readonly description?: string;
  readonly isReadOnly?: boolean;
  readonly isDisabled?: boolean;
  readonly isLocked?: boolean;
  readonly lockedReason?: string;
  readonly className?: string;
  readonly children?: ReactNode;
}

export function Switch({
  label,
  description,
  isLocked = false,
  lockedReason,
  className,
  isReadOnly,
  isDisabled,
  children,
  ...props
}: SwitchProps): React.JSX.Element {
  const isEffectiveDisabled = isDisabled || isLocked;
  const isEffectiveReadOnly = isReadOnly;

  return (
    <AriaSwitch
      {...props}
      {...(isEffectiveDisabled !== undefined ? { isDisabled: isEffectiveDisabled } : {})}
      {...(isEffectiveReadOnly !== undefined ? { isReadOnly: isEffectiveReadOnly } : {})}
      className={clsx("ui-switch", className)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "0.75rem",
        fontFamily: "var(--font-sans)",
        cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
        opacity: isEffectiveDisabled ? 0.6 : 1,
        fontSize: "0.875rem",
        color: "var(--color-content-primary)",
        userSelect: "none"
      }}
    >
      {({ isSelected, isFocusVisible }) => (
        <>
          <div
            style={{
              width: "2.5rem",
              height: "1.375rem",
              borderRadius: "9999px",
              backgroundColor: isSelected ? "var(--color-action-primary)" : "var(--color-border-default)",
              position: "relative",
              transition: "background-color 200ms ease",
              outline: isFocusVisible ? "2px solid var(--color-action-primary)" : "none",
              outlineOffset: "2px",
              flexShrink: 0
            }}
          >
            <div
              style={{
                width: "1.125rem",
                height: "1.125rem",
                borderRadius: "50%",
                backgroundColor: "var(--color-surface-card)",
                position: "absolute",
                top: "0.125rem",
                insetInlineStart: isSelected ? "1.25rem" : "0.125rem",
                transition: "inset-inline-start 200ms cubic-bezier(0.4, 0, 0.2, 1)",
                boxShadow: "0 1px 3px rgba(0,0,0,0.2)"
              }}
            />
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
    </AriaSwitch>
  );
}
