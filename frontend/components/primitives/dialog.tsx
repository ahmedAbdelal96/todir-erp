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
        zIndex: 2000,
        backdropFilter: "blur(3px)"
      }}
    >
      <AriaModal
        className="ui-modal"
        style={{
          position: "fixed",
          top: "50%",
          left: "50%",
          transform: "translate(-50%, -50%)",
          margin: 0,
          width: `min(${getMaxWidth()}, calc(100vw - 2rem))`,
          maxWidth: getMaxWidth(),
          outline: "none",
          zIndex: 2001,
          display: "flex",
          flexDirection: "column"
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
            maxHeight: "88vh",
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
