import React, { type ReactNode } from "react";
import {
  ModalOverlay as AriaModalOverlay,
  Modal as AriaModal,
  Dialog as AriaDialog,
  Heading as AriaHeading,
  type DialogProps as AriaDialogProps
} from "react-aria-components";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface DialogProps extends Omit<AriaDialogProps, "className"> {
  readonly title?: ReactNode;
  readonly description?: ReactNode;
  readonly footer?: ReactNode;
  readonly isDismissable?: boolean;
  readonly isOpen?: boolean;
  readonly onOpenChange?: (isOpen: boolean) => void;
  readonly onClose?: () => void;
  readonly size?: "small" | "medium" | "large" | "wide" | "fullscreen";
  readonly className?: string;
}

export function Dialog({
  title,
  description,
  footer,
  isOpen,
  onOpenChange,
  onClose,
  isDismissable = true,
  size = "medium",
  className,
  children,
  ...props
}: DialogProps): React.JSX.Element | null {
  const { t } = useTranslation("common");
  if (isOpen === false) {
    return null;
  }

  const handleOpenChange = (open: boolean) => {
    onOpenChange?.(open);
    if (!open) {
      onClose?.();
    }
  };

  const getMaxWidth = () => {
    switch (size) {
      case "small":
        return "24rem";
      case "medium":
        return "36rem";
      case "large":
        return "48rem";
      case "wide":
        return "84rem";
      case "fullscreen":
        return "92vw";
      default:
        return "36rem";
    }
  };

  return (
    <AriaModalOverlay
      {...(isOpen !== undefined ? { isOpen } : {})}
      {...(onOpenChange !== undefined || onClose !== undefined ? { onOpenChange: handleOpenChange } : {})}
      isDismissable={isDismissable}
      className="ui-modal-overlay"
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(15, 23, 42, 0.6)",
        zIndex: 1400,
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1.25rem",
        boxSizing: "border-box"
      }}
    >
      <AriaModal
        className="ui-modal"
        style={{
          width: size === "wide" ? "min(84rem, 82vw)" : `min(${getMaxWidth()}, calc(100vw - 2.5rem))`,
          maxWidth: getMaxWidth(),
          maxHeight: "calc(100vh - 2.5rem)",
          outline: "none",
          zIndex: 1401,
          display: "flex",
          flexDirection: "column",
          margin: "auto"
        }}
      >
        <AriaDialog
          {...props}
          className={clsx("ui-dialog", `ui-dialog--${size}`, className)}
          style={{
            backgroundColor: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg, 0.75rem)",
            border: "1px solid var(--color-border-default)",
            boxShadow: "0 25px 50px -12px rgba(15, 23, 42, 0.25)",
            width: "100%",
            maxHeight: "calc(100vh - 2.5rem)",
            display: "flex",
            flexDirection: "column",
            outline: "none",
            overflow: "hidden",
            margin: 0
          }}
        >
        {({ close }) => (
          <>
            {title && (
              <div
                className="ui-dialog-header"
                style={{
                  padding: "1rem 1.25rem",
                  borderBottom: "1px solid var(--color-border-default)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  flexShrink: 0,
                  backgroundColor: "var(--color-surface-card)",
                  zIndex: 10
                }}
              >
                <div>
                  <AriaHeading
                    slot="title"
                    style={{
                      margin: 0,
                      fontSize: "1.0625rem",
                      fontWeight: 600,
                      color: "var(--color-content-primary)"
                    }}
                  >
                    {title}
                  </AriaHeading>
                  {description && (
                    <p
                      style={{
                        margin: "0.25rem 0 0 0",
                        fontSize: "0.8125rem",
                        color: "var(--color-content-muted)"
                      }}
                    >
                      {description}
                    </p>
                  )}
                </div>
                {isDismissable && (
                  <button
                    type="button"
                    onClick={close}
                    style={{
                      background: "transparent",
                      border: "none",
                      cursor: "pointer",
                      padding: "0.25rem",
                      color: "var(--color-content-muted)"
                    }}
                    aria-label={t("components.closeDialog")}
                  >
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                      <path d="M1 1L13 13M1 13L13 1" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                    </svg>
                  </button>
                )}
              </div>
            )}
            <div
              className="ui-dialog-body"
              style={{
                padding: "1.25rem",
                overflowY: "auto",
                flex: 1,
                minHeight: 0,
                fontSize: "0.875rem",
                color: "var(--color-content-primary)"
              }}
            >
              {typeof children === "function" ? children({ close }) : children}
            </div>
            {footer && (
              <div
                className="ui-dialog-footer"
                style={{
                  padding: "0.875rem 1.25rem",
                  borderTop: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-subtle)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "flex-end",
                  gap: "0.625rem",
                  flexShrink: 0,
                  zIndex: 10
                }}
              >
                {footer}
              </div>
            )}
          </>
        )}
      </AriaDialog>
    </AriaModal>
  </AriaModalOverlay>
);
}
