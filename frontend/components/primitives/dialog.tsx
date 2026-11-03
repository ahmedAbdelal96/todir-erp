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
  readonly size?: "small" | "medium" | "large" | "fullscreen";
  readonly className?: string;
}

export function Dialog({
  title,
  description,
  footer,
  isOpen,
  onOpenChange,
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

  const getMaxWidth = () => {
    switch (size) {
      case "small":
        return "22rem";
      case "medium":
        return "30rem";
      case "large":
        return "40rem";
      case "fullscreen":
        return "92vw";
      default:
        return "30rem";
    }
  };

  return (
    <AriaModalOverlay
      {...(isOpen !== undefined ? { isOpen } : {})}
      {...(onOpenChange !== undefined ? { onOpenChange } : {})}
      isDismissable={isDismissable}
      className="ui-modal-overlay"
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(15, 23, 42, 0.6)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
        zIndex: 2000,
        backdropFilter: "blur(3px)"
      }}
    >
      <AriaModal
        className="ui-modal"
        style={{
          width: "100%",
          maxWidth: getMaxWidth(),
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          margin: "auto",
          outline: "none"
        }}
      >
        <AriaDialog
          {...props}
          className={clsx("ui-dialog", `ui-dialog--${size}`, className)}
          style={{
            backgroundColor: "var(--color-surface-card)",
            borderRadius: "var(--radius-lg, 0.5rem)",
            boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.15), 0 10px 10px -5px rgba(0, 0, 0, 0.04)",
            width: "100%",
            maxHeight: "90vh",
            display: "flex",
            flexDirection: "column",
            outline: "none",
            overflow: "hidden",
            margin: "auto"
          }}
        >
        {({ close }) => (
          <>
            {title && (
              <div
                style={{
                  padding: "1rem 1.25rem",
                  borderBottom: "1px solid var(--color-border-default)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between"
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
              style={{
                padding: "1.25rem",
                overflowY: "auto",
                flex: 1,
                fontSize: "0.875rem",
                color: "var(--color-content-primary)"
              }}
            >
              {typeof children === "function" ? children({ close }) : children}
            </div>
            {footer && (
              <div
                style={{
                  padding: "0.875rem 1.25rem",
                  borderTop: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-subtle)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "flex-end",
                  gap: "0.625rem"
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
