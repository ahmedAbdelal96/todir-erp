import React, { type ReactNode } from "react";
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

import { normalizeSearchQuery } from "./search-normalizer.ts";
export { normalizeSearchQuery };

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
  readonly isLoading?: boolean | undefined;
  readonly allowClear?: boolean | undefined;
  readonly showSearchHeader?: boolean | undefined;
  readonly onClear?: (() => void) | undefined;
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
  isLoading = false,
  allowClear = true,
  showSearchHeader = true,
  onClear,
  ...props
}: ComboBoxProps<T>): React.JSX.Element {
  const { t } = useTranslation("common");
  const [internalInput, setInternalInput] = React.useState(props.inputValue ?? "");
  const [isFocused, setIsFocused] = React.useState(false);
  const currentSearchTerm = props.inputValue !== undefined ? props.inputValue : internalInput;

  const handleInputChange = (val: string) => {
    setInternalInput(val);
    props.onInputChange?.(val);
  };

  const handleClear = () => {
    setInternalInput("");
    props.onInputChange?.("");
    onSelectionChange?.(null);
    onClear?.();
  };

  const searchPlaceholder = placeholder ?? t("components.searchPlaceholder");
  const isEffectiveDisabled = isDisabled || isLocked;

  // Determine if the current input value simply mirrors the selected item's label
  const isSelectedValueActive = React.useMemo(() => {
    if (!selectedKey || !options) return false;
    const selectedOpt = options.find((o) => (o as unknown as ComboBoxOption).id === String(selectedKey));
    return selectedOpt ? (selectedOpt as unknown as ComboBoxOption).label === currentSearchTerm : false;
  }, [selectedKey, options, currentSearchTerm]);

  // Fast Arabic-normalized local filtering
  const filteredOptions = React.useMemo(() => {
    if (!options) return undefined;
    if (!currentSearchTerm || currentSearchTerm.trim() === "" || isSelectedValueActive) {
      return options;
    }
    const term = normalizeSearchQuery(currentSearchTerm);
    return options.filter((item) => {
      const opt = item as unknown as ComboBoxOption;
      const matchLabel = normalizeSearchQuery(opt.label).includes(term);
      const matchDesc = opt.description ? normalizeSearchQuery(opt.description).includes(term) : false;
      const matchId = normalizeSearchQuery(opt.id).includes(term);
      return matchLabel || matchDesc || matchId;
    });
  }, [options, currentSearchTerm, isSelectedValueActive]);

  const displayOptions = filteredOptions ?? options;

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
              fontWeight: "600",
              color: "var(--color-content-primary, #0f172a)"
            }}
          >
            {label}
            {isRequired && <span style={{ color: "var(--color-status-danger, #ef4444)", marginInlineStart: "0.25rem" }}>*</span>}
          </AriaLabel>
          {isLocked && lockedReason && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              {lockedReason}
            </span>
          )}
        </div>
      )}

      {/* Trigger & Search Field */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          backgroundColor: isEffectiveDisabled ? "var(--color-surface-subtle, #f8fafc)" : "var(--color-surface-card, #ffffff)",
          border: `1px solid ${
            errorMessage
              ? "var(--color-status-danger, #ef4444)"
              : isFocused
              ? "var(--color-action-primary, #0284c7)"
              : "var(--color-border-default, #cbd5e1)"
          }`,
          borderRadius: "var(--radius-md, 0.375rem)",
          paddingInlineStart: "0.625rem",
          paddingInlineEnd: "0.375rem",
          height: size === "compact" ? "var(--density-field-height, 2rem)" : "var(--density-field-height, 2.5rem)",
          boxShadow: errorMessage
            ? "0 0 0 1px var(--color-status-danger, #ef4444)"
            : isFocused
            ? "0 0 0 2px rgba(2, 132, 199, 0.15)"
            : "none",
          gap: "0.375rem",
          transition: "border-color 150ms ease, box-shadow 150ms ease"
        }}
      >
        {/* Prominent Search Icon */}
        <span
          aria-hidden="true"
          style={{
            color: isFocused ? "var(--color-action-primary, #0284c7)" : "var(--color-content-muted, #94a3b8)",
            display: "flex",
            alignItems: "center",
            flexShrink: 0,
            transition: "color 150ms ease"
          }}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
        </span>

        {/* Search & Selection Input */}
        <AriaInput
          placeholder={searchPlaceholder}
          onFocus={(e) => {
            setIsFocused(true);
            // Auto-select text on focus so typing immediately searches fresh
            e.target.select();
          }}
          onBlur={() => setIsFocused(false)}
          style={{
            flex: 1,
            border: "none",
            outline: "none",
            backgroundColor: "transparent",
            color: isEffectiveDisabled ? "var(--color-content-muted, #94a3b8)" : "var(--color-content-primary, #0f172a)",
            fontSize: "0.875rem",
            fontFamily: "inherit",
            cursor: isEffectiveDisabled ? "not-allowed" : "text",
            width: "100%",
            minWidth: 0
          }}
        />

        {/* Loading Spinner */}
        {isLoading && (
          <span
            aria-label={t("components.loading", "جارٍ التحميل...")}
            style={{
              color: "var(--color-action-primary, #0284c7)",
              display: "flex",
              alignItems: "center",
              flexShrink: 0
            }}
          >
            <svg
              style={{
                width: "14px",
                height: "14px",
                animation: "spin 0.8s linear infinite"
              }}
              viewBox="0 0 24 24"
              fill="none"
            >
              <circle
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="3"
                strokeDasharray="32"
                strokeLinecap="round"
              />
            </svg>
          </span>
        )}

        {/* One-click Clear Button (×) */}
        {allowClear && (currentSearchTerm || selectedKey) && !isEffectiveDisabled && !isReadOnly && (
          <button
            type="button"
            tabIndex={-1}
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              handleClear();
            }}
            title={t("components.clearSelection", "مسح التحديد")}
            style={{
              border: "none",
              background: "transparent",
              color: "var(--color-content-muted, #94a3b8)",
              cursor: "pointer",
              padding: "0.125rem",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              borderRadius: "var(--radius-sm, 0.25rem)",
              flexShrink: 0,
              transition: "color 150ms ease, background-color 150ms ease"
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.color = "var(--color-status-danger, #ef4444)";
              e.currentTarget.style.backgroundColor = "rgba(239, 68, 68, 0.08)";
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.color = "var(--color-content-muted, #94a3b8)";
              e.currentTarget.style.backgroundColor = "transparent";
            }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}

        {/* Chevron Dropdown Toggle */}
        <AriaButton
          aria-label={t("components.selectOption", "خيارات القائمة")}
          style={{
            border: "none",
            backgroundColor: "transparent",
            cursor: isEffectiveDisabled ? "not-allowed" : "pointer",
            padding: "0.25rem 0.375rem",
            color: isFocused ? "var(--color-action-primary, #0284c7)" : "var(--color-content-muted, #94a3b8)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
            transition: "color 150ms ease"
          }}
        >
          <svg width="12" height="8" viewBox="0 0 12 8" fill="none">
            <path d="M1 1.5L6 6.5L11 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </AriaButton>
      </div>

      {description && !errorMessage && (
        <AriaText slot="description" style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
          {description}
        </AriaText>
      )}

      {errorMessage && (
        <AriaFieldError style={{ fontSize: "0.75rem", color: "var(--color-status-danger, #ef4444)" }}>
          {errorMessage}
        </AriaFieldError>
      )}

      {/* Dropdown Popover */}
      <AriaPopover
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-md, 0.375rem)",
          boxShadow: "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)",
          minWidth: "var(--trigger-width)",
          maxHeight: "18rem",
          overflowY: "auto",
          zIndex: 2500
        }}
      >
        {/* Dedicated Search Status Header ("مكان وتفاصيل البحث") */}
        {showSearchHeader && (
          <div
            style={{
              padding: "0.375rem 0.625rem",
              borderBottom: "1px solid var(--color-border-subtle, #f1f5f9)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              fontSize: "0.75rem",
              color: "var(--color-content-muted, #64748b)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              borderTopLeftRadius: "var(--radius-md, 0.375rem)",
              borderTopRightRadius: "var(--radius-md, 0.375rem)",
              userSelect: "none"
            }}
          >
            <span style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              {currentSearchTerm && !isSelectedValueActive ? (
                <span>
                  {t("components.searchResultsFor", "نتائج البحث عن:")} <strong style={{ color: "var(--color-content-primary, #0f172a)" }}>{currentSearchTerm}</strong>
                </span>
              ) : (
                <span>{t("components.searchPrompt", "اكتب للبحث المباشر...")}</span>
              )}
            </span>
            {displayOptions && (
              <span
                style={{
                  fontWeight: 600,
                  fontSize: "0.6875rem",
                  backgroundColor: "var(--color-border-subtle, #e2e8f0)",
                  color: "var(--color-content-primary, #1e293b)",
                  padding: "0.0625rem 0.375rem",
                  borderRadius: "9999px"
                }}
              >
                {displayOptions.length}
              </span>
            )}
          </div>
        )}

        {/* Options List */}
        <AriaListBox
          {...(displayOptions !== undefined ? { items: displayOptions } : {})}
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
                <ComboBoxItem
                  id={opt.id}
                  textValue={`${opt.label} ${opt.description ?? ""} ${opt.id}`}
                  {...(opt.isDisabled !== undefined ? { isDisabled: opt.isDisabled } : {})}
                >
                  <div style={{ display: "flex", flexDirection: "column", width: "100%" }}>
                    <span style={{ fontWeight: 500 }}>{opt.label}</span>
                    {opt.description && (
                      <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)", marginTop: "0.125rem" }}>
                        {opt.description}
                      </span>
                    )}
                  </div>
                </ComboBoxItem>
              );
            }
          )}
        </AriaListBox>

        {/* Empty State */}
        {displayOptions && displayOptions.length === 0 && (
          <div
            style={{
              padding: "1rem 1.25rem",
              textAlign: "center",
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: "0.5rem"
            }}
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
              <line x1="8" y1="11" x2="14" y2="11" />
            </svg>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {noResultsMessage ?? t("components.noOptionsFound", "لا توجد نتائج مطابقة")}
            </span>
            {currentSearchTerm && (
              <button
                type="button"
                onClick={handleClear}
                style={{
                  fontSize: "0.75rem",
                  color: "var(--color-action-primary, #0284c7)",
                  background: "none",
                  border: "none",
                  cursor: "pointer",
                  textDecoration: "underline",
                  padding: 0
                }}
              >
                {t("components.clearSearch", "مسح البحث")}
              </button>
            )}
          </div>
        )}

        {/* Quick Create Action */}
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
          ? "var(--color-action-primary, #0284c7)"
          : isFocused
          ? "var(--color-surface-subtle, #f1f5f9)"
          : "transparent",
        color: isSelected
          ? "var(--color-action-primary-content, #ffffff)"
          : "var(--color-content-primary, #0f172a)",
        outline: "none",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        transition: "background-color 100ms ease"
      })}
    >
      {children}
    </AriaListBoxItem>
  );
}
