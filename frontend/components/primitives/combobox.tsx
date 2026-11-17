import React, { type ReactNode, type Key } from "react";
import {
  ComboBox as AriaComboBox,
  Input as AriaInput,
  Button as AriaButton,
  Label as AriaLabel,
  Text as AriaText,
  FieldError as AriaFieldError,
  Popover as AriaPopover,
  ListBox as AriaListBox,
  ListBoxItem as AriaListBoxItem,
  type ComboBoxProps as AriaComboBoxProps,
  type ListBoxItemProps as AriaListBoxItemProps
} from "react-aria-components";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export type ComboBoxKey = string | number;

export interface ComboBoxOption {
  readonly id: string;
  readonly label: string;
  readonly description?: string | undefined;
  readonly isDisabled?: boolean | undefined;
}

export interface ComboBoxProps<T extends object = ComboBoxOption>
  extends Omit<
    AriaComboBoxProps<T>,
    | "className"
    | "children"
    | "placeholder"
    | "isDisabled"
    | "isInvalid"
    | "isRequired"
    | "isReadOnly"
    | "selectedKey"
    | "defaultSelectedKey"
    | "onSelectionChange"
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
  readonly isReadOnly?: boolean | undefined;
  readonly selectedKey?: ComboBoxKey | null | undefined;
  readonly defaultSelectedKey?: ComboBoxKey | null | undefined;
  readonly onSelectionChange?: ((key: ComboBoxKey | null) => void) | undefined;
  readonly options?: readonly T[] | undefined;
  readonly onCreateNew?: ((searchTerm: string) => void) | undefined;
  readonly createActionLabel?: string | ((searchTerm: string) => string) | undefined;
  readonly noResultsMessage?: string | undefined;
  readonly children?: ReactNode | ((item: T) => ReactNode) | undefined;
  readonly className?: string | undefined;
}

export function ComboBox<T extends object = ComboBoxOption>({
  label,
  description,
  errorMessage,
  placeholder,
  size = "comfortable",
  isLocked = false,
  lockedReason,
  options,
  onCreateNew,
  createActionLabel,
  noResultsMessage,
  menuTrigger = "focus",
  children,
  className,
  isDisabled,
  isRequired,
  isReadOnly,
  selectedKey,
  defaultSelectedKey,
  onSelectionChange,
  ...props
}: ComboBoxProps<T>): React.JSX.Element {
  const { t } = useTranslation("common");
  const [internalInput, setInternalInput] = React.useState(props.inputValue ?? "");
  const currentSearchTerm = props.inputValue !== undefined ? props.inputValue : internalInput;

  const handleInputChange = (val: string) => {
    setInternalInput(val);
    props.onInputChange?.(val);
  };

  const searchPlaceholder = placeholder ?? t("components.searchPlaceholder");
  const isEffectiveDisabled = isDisabled || isLocked;

  return (
    <AriaComboBox
      {...props}
      menuTrigger={menuTrigger}
      onInputChange={handleInputChange}
      {...(isEffectiveDisabled !== undefined ? { isDisabled: isEffectiveDisabled } : {})}
      {...(isRequired !== undefined ? { isRequired } : {})}
      {...(isReadOnly !== undefined ? { isReadOnly } : {})}
      {...(selectedKey !== undefined ? { selectedKey } : {})}
      {...(defaultSelectedKey !== undefined ? { defaultSelectedKey } : {})}
      {...(onSelectionChange !== undefined ? { onSelectionChange } : {})}
      isInvalid={Boolean(errorMessage)}
      className={clsx("ui-combobox", `ui-combobox--${size}`, className)}
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

      <div
        style={{
          display: "flex",
          alignItems: "center",
          backgroundColor: isEffectiveDisabled ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
          border: `1px solid ${errorMessage ? "var(--color-status-danger)" : "var(--color-border-default)"}`,
          borderRadius: "var(--radius-md)",
          paddingInlineStart: "0.75rem",
          paddingInlineEnd: "0.25rem",
          height: size === "compact" ? "var(--density-field-height, 2rem)" : "var(--density-field-height, 2.5rem)",
          boxShadow: errorMessage ? "0 0 0 1px var(--color-status-danger)" : "none"
        }}
      >
        <AriaInput
          placeholder={searchPlaceholder}
          style={{
            flex: 1,
            border: "none",
            outline: "none",
            backgroundColor: "transparent",
            color: isEffectiveDisabled ? "var(--color-content-muted)" : "var(--color-content-primary)",
            fontSize: "0.875rem",
            fontFamily: "inherit",
            cursor: isEffectiveDisabled ? "not-allowed" : "text",
            width: "100%"
          }}
        />
        <AriaButton
          style={{
            border: "none",
            backgroundColor: "transparent",
            cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
            padding: "0.25rem 0.5rem",
            color: "var(--color-content-muted)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center"
          }}
        >
          <svg width="12" height="8" viewBox="0 0 12 8" fill="none">
            <path d="M1 1.5L6 6.5L11 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </AriaButton>
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

      <AriaPopover
        style={{
          backgroundColor: "var(--color-surface-card)",
          border: "1px solid var(--color-border-default)",
          borderRadius: "var(--radius-md)",
          boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
          minWidth: "var(--trigger-width)",
          maxHeight: "15rem",
          overflowY: "auto",
          zIndex: 2500
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
              const opt = item as unknown as ComboBoxOption;
              return (
                <ComboBoxItem id={opt.id} textValue={opt.label} {...(opt.isDisabled !== undefined ? { isDisabled: opt.isDisabled } : {})}>
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    <span>{opt.label}</span>
                    {opt.description && (
                      <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
                        {opt.description}
                      </span>
                    )}
                  </div>
                </ComboBoxItem>
              );
            }
          )}
        </AriaListBox>

        {options && options.length === 0 && (
          <div
            style={{
              padding: "0.75rem 1rem",
              textAlign: "center",
              fontSize: "0.8125rem",
              color: "var(--color-content-muted, #64748b)"
            }}
          >
            {noResultsMessage ?? t("components.noOptionsFound")}
          </div>
        )}

        {onCreateNew && (
          <div
            style={{
              padding: "0.375rem 0.5rem",
              borderTop: "1px solid var(--color-border-subtle, #e2e8f0)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)"
            }}
          >
            <button
              type="button"
              onMouseDown={(e) => {
                e.preventDefault();
              }}
              onClick={() => {
                onCreateNew(currentSearchTerm);
              }}
              style={{
                width: "100%",
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-sm, 0.25rem)",
                border: "1px dashed var(--color-brand-primary, #102a43)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                color: "var(--color-brand-primary, #102a43)",
                fontSize: "0.8125rem",
                fontWeight: 700,
                cursor: "pointer",
                textAlign: "start",
                transition: "all 150ms ease"
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.backgroundColor = "rgba(16, 42, 67, 0.06)";
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.backgroundColor = "var(--color-surface-card, #ffffff)";
              }}
            >
              <span style={{ fontSize: "1rem", lineHeight: 1, fontWeight: 800 }}>+</span>
              <span>
                {createActionLabel
                  ? typeof createActionLabel === "function"
                    ? createActionLabel(currentSearchTerm)
                    : createActionLabel
                  : t("components.createNewOption", { name: currentSearchTerm ? `"${currentSearchTerm}"` : "" })}
              </span>
            </button>
          </div>
        )}
      </AriaPopover>
    </AriaComboBox>
  );
}

export interface ComboBoxItemProps extends Omit<AriaListBoxItemProps, "className"> {
  readonly className?: string | undefined;
}

export function ComboBoxItem({ className, children, ...props }: ComboBoxItemProps): React.JSX.Element {
  return (
    <AriaListBoxItem
      {...props}
      className={clsx("ui-combobox-item", className)}
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
