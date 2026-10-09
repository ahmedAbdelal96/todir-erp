import React, { type ReactNode } from "react";
import {
  Menu as AriaMenu,
  MenuItem as AriaMenuItem,
  MenuTrigger as AriaMenuTrigger,
  Section as AriaSection,
  Header as AriaHeader,
  Separator as AriaSeparator,
  Popover as AriaPopover,
  type MenuProps as AriaMenuProps,
  type MenuItemProps as AriaMenuItemProps
} from "react-aria-components";
import clsx from "clsx";

export { AriaMenuTrigger as MenuTrigger };

export interface MenuProps<T extends object> extends Omit<AriaMenuProps<T>, "className"> {
  readonly className?: string;
}

export function Menu<T extends object>({ className, children, ...props }: MenuProps<T>): React.JSX.Element {
  return (
    <AriaPopover
      style={{
        backgroundColor: "var(--color-surface-card)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-md)",
        boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1)",
        minWidth: "10rem",
        zIndex: 1500,
        outline: "none"
      }}
    >
      <AriaMenu
        {...props}
        className={clsx("ui-menu", className)}
        style={{
          padding: "0.25rem",
          outline: "none",
          display: "flex",
          flexDirection: "column",
          gap: "0.125rem"
        }}
      >
        {children}
      </AriaMenu>
    </AriaPopover>
  );
}

export interface MenuItemProps extends Omit<AriaMenuItemProps, "className"> {
  readonly icon?: ReactNode;
  readonly isDestructive?: boolean;
  readonly className?: string;
}

export function MenuItem({
  icon,
  isDestructive = false,
  className,
  children,
  ...props
}: MenuItemProps): React.JSX.Element {
  return (
    <AriaMenuItem
      {...props}
      className={clsx("ui-menu-item", className)}
      style={({ isFocused, isDisabled }) => ({
        padding: "0.5rem 0.75rem",
        borderRadius: "var(--radius-sm, 0.25rem)",
        fontSize: "0.875rem",
        cursor: isDisabled ? "not-allowed" : "pointer",
        opacity: isDisabled ? 0.5 : 1,
        backgroundColor: isFocused
          ? isDestructive
            ? "var(--color-status-danger-subtle, rgba(239, 68, 68, 0.1))"
            : "var(--color-surface-subtle)"
          : "transparent",
        color: isDestructive
          ? "var(--color-status-danger)"
          : "var(--color-content-primary)",
        outline: "none",
        display: "flex",
        alignItems: "center",
        gap: "0.5rem"
      })}
    >
      {icon && <span style={{ flexShrink: 0 }}>{icon}</span>}
      <span style={{ flex: 1 }}>{children as ReactNode}</span>
    </AriaMenuItem>
  );
}

export function MenuSection({
  title,
  children
}: {
  readonly title?: string;
  readonly children: ReactNode;
}): React.JSX.Element {
  return (
    <AriaSection style={{ display: "flex", flexDirection: "column", gap: "0.125rem" }}>
      {title && (
        <AriaHeader
          style={{
            padding: "0.375rem 0.75rem",
            fontSize: "0.75rem",
            fontWeight: 600,
            color: "var(--color-content-muted)",
            textTransform: "uppercase"
          }}
        >
          {title}
        </AriaHeader>
      )}
      {children}
    </AriaSection>
  );
}

export function MenuSeparator(): React.JSX.Element {
  return (
    <AriaSeparator
      style={{
        height: "1px",
        backgroundColor: "var(--color-border-default)",
        margin: "0.25rem 0"
      }}
    />
  );
}
