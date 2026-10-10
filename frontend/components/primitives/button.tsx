import React, { type ReactNode, type MouseEvent } from "react";
import {
  Button as AriaButton,
  type ButtonProps as AriaButtonProps,
  type PressEvent
} from "react-aria-components";
import clsx from "clsx";

export type ButtonVariant = "primary" | "secondary" | "tertiary" | "ghost" | "danger";
export type ButtonSize = "comfortable" | "compact";

export interface ButtonProps
  extends Omit<
    AriaButtonProps,
    "className" | "isDisabled" | "onClick" | "onPress" | "type"
  > {
  readonly variant?: ButtonVariant | undefined;
  readonly size?: ButtonSize | undefined;
  readonly isLoading?: boolean | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly startIcon?: ReactNode | undefined;
  readonly endIcon?: ReactNode | undefined;
  readonly className?: string | undefined;
  readonly children?: ReactNode | undefined;
  readonly type?: "button" | "submit" | "reset" | undefined;
  readonly onClick?: ((e: MouseEvent<any>) => void) | undefined;
  readonly onPress?: ((e: PressEvent) => void) | undefined;
}

export function Button({
  variant = "primary",
  size = "comfortable",
  isLoading = false,
  isDisabled = false,
  startIcon,
  endIcon,
  className,
  children,
  onClick,
  onPress,
  type = "button",
  ...props
}: ButtonProps): React.JSX.Element {
  const isEffectiveDisabled = isDisabled || isLoading;

  return (
    <AriaButton
      {...props}
      {...(isEffectiveDisabled !== undefined ? { isDisabled: isEffectiveDisabled } : {})}
      {...(onClick !== undefined ? { onClick } : {})}
      {...(onPress !== undefined ? { onPress } : {})}
      {...(type !== undefined ? { type } : {})}
      className={clsx(
        "ui-button",
        `ui-button--${variant}`,
        `ui-button--${size}`,
        {
          "ui-button--loading": isLoading,
          "ui-button--disabled": isEffectiveDisabled
        },
        className
      )}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "0.5rem",
        fontWeight: "500",
        fontFamily: "var(--font-sans)",
        borderRadius: "var(--radius-md)",
        cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
        opacity: isEffectiveDisabled ? 0.6 : 1,
        transition: "background-color 150ms ease, border-color 150ms ease, box-shadow 150ms ease",
        height: size === "compact" ? "var(--density-field-height, 2rem)" : "var(--density-field-height, 2.5rem)",
        paddingInline: size === "compact" ? "var(--density-button-padding-inline, 0.75rem)" : "var(--density-button-padding-inline, 1rem)",
        borderWidth: "1px",
        borderStyle: "solid",
        outline: "none",
        ...getVariantStyles(variant)
      }}
    >
      {isLoading ? (
        <span
          className="ui-button__spinner"
          aria-hidden="true"
          style={{
            display: "inline-block",
            width: "1em",
            height: "1em",
            border: "2px solid currentColor",
            borderRightColor: "transparent",
            borderRadius: "50%",
            animation: "spin 0.6s linear infinite"
          }}
        />
      ) : (
        startIcon && <span className="ui-button__icon-start">{startIcon}</span>
      )}
      <span className="ui-button__content">{children}</span>
      {!isLoading && endIcon && <span className="ui-button__icon-end">{endIcon}</span>}
    </AriaButton>
  );
}

export interface IconButtonProps extends Omit<ButtonProps, "children"> {
  readonly "aria-label": string;
  readonly icon: ReactNode;
}

export function IconButton({
  icon,
  "aria-label": ariaLabel,
  size = "comfortable",
  ...props
}: IconButtonProps): React.JSX.Element {
  return (
    <Button
      {...props}
      size={size}
      aria-label={ariaLabel}
      style={{
        paddingInline: size === "compact" ? "0.5rem" : "0.75rem",
        minWidth: size === "compact" ? "2rem" : "2.5rem"
      }}
    >
      {icon}
    </Button>
  );
}

function getVariantStyles(variant: ButtonVariant): React.CSSProperties {
  switch (variant) {
    case "primary":
      return {
        backgroundColor: "var(--color-action-primary)",
        borderColor: "var(--color-action-primary)",
        color: "var(--color-action-primary-content)"
      };
    case "secondary":
      return {
        backgroundColor: "var(--color-action-secondary)",
        borderColor: "var(--color-border-default)",
        color: "var(--color-action-secondary-content)"
      };
    case "danger":
      return {
        backgroundColor: "var(--color-status-danger)",
        borderColor: "var(--color-status-danger)",
        color: "var(--color-content-inverse)"
      };
    case "tertiary":
      return {
        backgroundColor: "var(--color-surface-subtle)",
        borderColor: "transparent",
        color: "var(--color-content-primary)"
      };
    case "ghost":
      return {
        backgroundColor: "transparent",
        borderColor: "transparent",
        color: "var(--color-content-primary)"
      };
  }
}
