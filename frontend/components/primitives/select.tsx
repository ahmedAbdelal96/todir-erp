import React, { type ReactNode, type Key } from "react";
import {
  Select as AriaSelect,
  Button as AriaButton,
  SelectValue as AriaSelectValue,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  Popover as AriaPopover,
  ListBox as AriaListBox,
  ListBoxItem as AriaListBoxItem,
  type SelectProps as AriaSelectProps,
  type ListBoxItemProps as AriaListBoxItemProps
} from "react-aria-components";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export type SelectKey = string | number;

export interface SelectOption {
  readonly id: string;
  readonly label: string;
  readonly description?: string | undefined;
  readonly isDisabled?: boolean | undefined;
}

export interface SelectProps<T extends object = SelectOption>
  extends Omit<
    AriaSelectProps<T>,
    | "className"
    | "children"
    | "placeholder"
    | "isDisabled"
    | "isInvalid"
    | "isRequired"
    | "selectedKey"
    | "defaultSelectedKey"
  > {
  readonly label?: string | undefined;
  readonly description?: string | undefined;
  readonly errorMessage?: string | undefined;
  readonly placeholder?: string | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly isLocked?: boolean | undefined;
  readonly lockedReason?: string | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly isRequired?: boolean | undefined;
  readonly selectedKey?: SelectKey | null | undefined;
  readonly defaultSelectedKey?: SelectKey | null | undefined;
  readonly options?: readonly T[] | undefined;
  readonly children?: ReactNode | ((item: T) => ReactNode) | undefined;
  readonly className?: string | undefined;
}

export function Select<T extends object = SelectOption>({
  label,
  description,
  errorMessage,
  placeholder,
  size = "comfortable",
  isLocked = false,
  lockedReason,
  options,
  children,
  className,
  isDisabled,
  isRequired,
  selectedKey,
  defaultSelectedKey,
  ...props
}: SelectProps<T>): React.JSX.Element {
  const { t } = useTranslation("common");
  const selectPlaceholder = placeholder ?? t("components.selectOption");
  const isEffectiveDisabled = isDisabled || isLocked;

  return (
    <AriaSelect
      {...props}
      {...(isEffectiveDisabled !== undefined ? { isDisabled: isEffectiveDisabled } : {})}
      {...(isRequired !== undefined ? { isRequired } : {})}
      {...(selectedKey !== undefined ? { selectedKey } : {})}
      {...(defaultSelectedKey !== undefined ? { defaultSelectedKey } : {})}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-select", `ui-select--${size}`, className)}
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
            {isRequired && <span style={{ color: "var(--color-status-danger)", marginInlineStart: "0.25rem" }}>*</span>}
          </AriaLabel>
          {isLocked && lockedReason && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {lockedReason}
            </span>
          )}
        </div>
      )}

      <AriaButton
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          backgroundColor: isEffectiveDisabled ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
          border: `1px solid ${errorMessage ? "var(--color-status-danger)" : "var(--color-border-default)"}`,
          borderRadius: "var(--radius-md)",
          paddingInline: "0.75rem",
          height: size === "compact" ? "var(--density-field-height, 2rem)" : "var(--density-field-height, 2.5rem)",
          fontSize: "0.875rem",
          color: isEffectiveDisabled ? "var(--color-content-muted)" : "var(--color-content-primary)",
          cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
          outline: "none",
          width: "100%",
          boxShadow: errorMessage ? "0 0 0 1px var(--color-status-danger)" : "none"
        }}
      >
        <AriaSelectValue
          style={{
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap"
          }}
        >
          {({ defaultChildren, isPlaceholder }) =>
            isPlaceholder ? (
              <span style={{ color: "var(--color-content-muted)" }}>{selectPlaceholder}</span>
            ) : (
              defaultChildren
            )
          }
        </AriaSelectValue>
        <svg
          width="12"
          height="8"
          viewBox="0 0 12 8"
          fill="none"
          style={{ marginInlineStart: "0.5rem", flexShrink: 0, opacity: 0.7 }}
        >
          <path d="M1 1.5L6 6.5L11 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </AriaButton>

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

      <AriaPopover
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
          minWidth: "var(--trigger-width)",
          maxHeight: "15rem",
          overflowY: "auto",
          zIndex: 1000
        }}
      >
        <AriaListBox
          {...(options !== undefined ? { items: options } : {})}
          style={{
            padding: "0.25rem",
            outline: "none",
            display: "flex",
            flexDirection: "column",
            gap: "0.125rem"
          }}
        >
          {children ? (
            children
          ) : (
            (item: T) => {
              const opt = item as unknown as SelectOption;
              return (
                <SelectItem id={opt.id} textValue={opt.label} {...(opt.isDisabled !== undefined ? { isDisabled: opt.isDisabled } : {})}>
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    <span>{opt.label}</span>
                    {opt.description && (
                      <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        {opt.description}
                      </span>
                    )}
                  </div>
                </SelectItem>
              );
            }
          )}
        </AriaListBox>
      </AriaPopover>
    </AriaSelect>
  );
}

export interface SelectItemProps extends Omit<AriaListBoxItemProps, "className"> {
  readonly className?: string | undefined;
}

export function SelectItem({ className, children, ...props }: SelectItemProps): React.JSX.Element {
  return (
    <AriaListBoxItem
      {...props}
      className={clsx("ui-select-item", className)}
      style={({ isFocused, isSelected, isDisabled }) => ({
        padding: "0.5rem 0.75rem",
        borderRadius: "var(--radius-sm, 0.25rem)",
        fontSize: "0.875rem",
        cursor: isDisabled ? "not-allowed" : "pointer",
        opacity: isDisabled ? 0.5 : 1,
        backgroundColor: isSelected
          ? "var(--color-action-primary)"
          : isFocused
          ? "var(--color-surface-subtle)"
          : "transparent",
        color: isSelected
          ? "var(--color-action-primary-content)"
          : "var(--color-content-primary)",
        outline: "none",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between"
      })}
    >
      {children}
    </AriaListBoxItem>
  );
}
