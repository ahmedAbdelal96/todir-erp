import React, { type ReactNode } from "react";
import {
  Tabs as AriaTabs,
  TabList as AriaTabList,
  Tab as AriaTab,
  TabPanel as AriaTabPanel,
  type TabsProps as AriaTabsProps,
  type TabListProps as AriaTabListProps,
  type TabProps as AriaTabProps,
  type TabPanelProps as AriaTabPanelProps
} from "react-aria-components";
import clsx from "clsx";

export interface TabsProps extends Omit<AriaTabsProps, "className"> {
  readonly className?: string;
}

export function Tabs({ className, children, ...props }: TabsProps): React.JSX.Element {
  return (
    <AriaTabs
      {...props}
      className={clsx("ui-tabs", className)}
      style={{
        display: "flex",
        flexDirection: props.orientation === "vertical" ? "row" : "column",
        gap: "1rem",
        fontFamily: "var(--font-sans)"
      }}
    >
      {children}
    </AriaTabs>
  );
}

export interface TabListProps<T extends object> extends Omit<AriaTabListProps<T>, "className"> {
  readonly className?: string;
}

export function TabList<T extends object>({ className, children, ...props }: TabListProps<T>): React.JSX.Element {
  return (
    <AriaTabList
      {...props}
      className={clsx("ui-tab-list", className)}
      style={{
        display: "flex",
        gap: "0.5rem",
        borderBottom: "1px solid var(--color-border-default)",
        paddingBottom: "0.25rem",
        outline: "none",
        overflowX: "auto",
        maxWidth: "100%",
        scrollbarWidth: "none",
        WebkitOverflowScrolling: "touch"
      }}
    >
      {children}
    </AriaTabList>
  );
}

export interface TabProps extends Omit<AriaTabProps, "className"> {
  readonly icon?: ReactNode;
  readonly badge?: ReactNode;
  readonly className?: string;
}

export function Tab({ icon, badge, className, children, ...props }: TabProps): React.JSX.Element {
  return (
    <AriaTab
      {...props}
      className={clsx("ui-tab", className)}
      style={({ isSelected, isFocused, isDisabled }) => ({
        display: "inline-flex",
        alignItems: "center",
        flexShrink: 0,
        whiteSpace: "nowrap",
        gap: "0.5rem",
        padding: "0.5rem 0.75rem",
        fontSize: "0.875rem",
        fontWeight: isSelected ? 600 : 500,
        color: isSelected
          ? "var(--color-action-primary)"
          : isDisabled
          ? "var(--color-content-muted)"
          : "var(--color-content-secondary)",
        borderBottom: isSelected
          ? "2px solid var(--color-action-primary)"
          : "2px solid transparent",
        marginBottom: "-0.25rem",
        cursor: isDisabled ? "not-allowed" : "pointer",
        opacity: isDisabled ? 0.5 : 1,
        outline: isFocused ? "2px solid var(--color-action-primary)" : "none",
        outlineOffset: "2px",
        transition: "color 150ms ease, border-color 150ms ease"
      })}
    >
      {icon && <span style={{ flexShrink: 0 }}>{icon}</span>}
      <span>{children as ReactNode}</span>
      {badge && <span style={{ marginInlineStart: "0.25rem" }}>{badge}</span>}
    </AriaTab>
  );
}

export interface TabPanelProps extends Omit<AriaTabPanelProps, "className"> {
  readonly className?: string;
}

export function TabPanel({ className, children, ...props }: TabPanelProps): React.JSX.Element {
  return (
    <AriaTabPanel
      {...props}
      className={clsx("ui-tab-panel", className)}
      style={{
        outline: "none",
        flex: 1
      }}
    >
      {children}
    </AriaTabPanel>
  );
}
