import React, { type ReactNode } from "react";
import {
  Modal as AriaModal,
  Dialog as AriaDialog,
  Heading as AriaHeading,
  type DialogProps as AriaDialogProps
} from "react-aria-components";
import clsx from "clsx";

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
  if (isOpen === false) {
    return null;
  }

  const getMaxWidth = () => {
    switch (size) {
      case "small":
        return "24rem";
      case "medium":
        return "36rem";
      case "large":
        return "48rem";
      case "fullscreen":
        return "95vw";
    }
  };

  return (
    <AriaModal
      {...(isOpen !== undefined ? { isOpen } : {})}
      {...(onOpenChange !== undefined ? { onOpenChange } : {})}
      isDismissable={isDismissable}
      className="ui-modal-overlay"
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
        zIndex: 2000,
        backdropFilter: "blur(2px)"
      }}
    >
      <AriaDialog
        {...props}
        className={clsx("ui-dialog", `ui-dialog--${size}`, className)}
        style={{
          backgroundColor: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg, 0.5rem)",
          boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)",
          width: "100%",
          maxWidth: getMaxWidth(),
          maxHeight: "90vh",
          display: "flex",
          flexDirection: "column",
          outline: "none",
          overflow: "hidden"
        }}
      >
        {({ close }) => (
          <>
            {title && (
              <div
                style={{
                  padding: "1.25rem 1.5rem",
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
                      fontSize: "1.125rem",
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
                        fontSize: "0.875rem",
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
                    aria-label="Close dialog"
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
                padding: "1.5rem",
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
                  padding: "1rem 1.5rem",
                  borderTop: "1px solid var(--color-border-default)",
                  backgroundColor: "var(--color-surface-subtle)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "flex-end",
                  gap: "0.75rem"
                }}
              >
                {footer}
              </div>
            )}
          </>
        )}
      </AriaDialog>
    </AriaModal>
  );
}
