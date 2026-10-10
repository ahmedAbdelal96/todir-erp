import React, { type ReactNode } from "react";
import { Dialog } from "./dialog.tsx";
import { Button } from "./button.tsx";

export interface ConfirmDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (isOpen: boolean) => void;
  readonly title: string;
  readonly description: string | ReactNode;
  readonly confirmLabel?: string | undefined;
  readonly cancelLabel?: string | undefined;
  readonly variant?: "danger" | "primary" | undefined;
  readonly onConfirm: () => void | Promise<void>;
  readonly onCancel?: (() => void) | undefined;
  readonly isPending?: boolean | undefined;
}

export function ConfirmDialog({
  isOpen,
  onOpenChange,
  title,
  description,
  confirmLabel = "تأكيد",
  cancelLabel = "إلغاء",
  variant = "danger",
  onConfirm,
  onCancel,
  isPending = false
}: ConfirmDialogProps): React.JSX.Element {
  const handleCancel = () => {
    if (isPending) return;
    onCancel?.();
    onOpenChange(false);
  };

  const handleConfirm = async () => {
    if (isPending) return;
    await onConfirm();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!isPending) {
          if (!open) handleCancel();
          else onOpenChange(true);
        }
      }}
      title={title}
      description={typeof description === "string" ? description : undefined}
      size="small"
    >
      {typeof description !== "string" && <div style={{ marginBottom: "1rem" }}>{description}</div>}

      <div
        style={{
          display: "flex",
          justifyContent: "flex-end",
          gap: "0.75rem",
          marginTop: "1.5rem"
        }}
      >
        <Button
          variant="secondary"
          onPress={handleCancel}
          isDisabled={isPending}
        >
          {cancelLabel}
        </Button>
        <Button
          variant={variant === "danger" ? "danger" : "primary"}
          onPress={handleConfirm}
          isDisabled={isPending}
        >
          {isPending ? "جارٍ التنفيذ..." : confirmLabel}
        </Button>
      </div>
    </Dialog>
  );
}
