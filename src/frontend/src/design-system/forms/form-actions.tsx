import React, { type ReactNode } from "react";
import { Button } from "../primitives/button.tsx";
import clsx from "clsx";

export interface FormActionsProps {
  readonly submitLabel?: string | undefined;
  readonly cancelLabel?: string | undefined;
  readonly isSubmitting?: boolean | undefined;
  readonly isDirty?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
  readonly onCancel?: (() => void) | undefined;
  readonly onDelete?: (() => void) | undefined;
  readonly deleteLabel?: string | undefined;
  readonly extraActions?: ReactNode | undefined;
  readonly className?: string | undefined;
}

export function FormActions({
  submitLabel = "Save Changes",
  cancelLabel = "Cancel",
  isSubmitting = false,
  isDirty = true,
  isLocked = false,
  onCancel,
  onDelete,
  deleteLabel = "Delete",
  extraActions,
  className
}: FormActionsProps): React.JSX.Element {
  return (
    <div
      className={clsx("ui-form-actions", className)}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        paddingTop: "1rem",
        borderTop: "1px solid var(--color-border-default)",
        gap: "0.75rem",
        flexWrap: "wrap"
      }}
    >
      <div>
        {onDelete && (
          <Button
            type="button"
            variant="danger"
            onClick={onDelete}
            isDisabled={isSubmitting || isLocked}
          >
            {deleteLabel}
          </Button>
        )}
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
        {extraActions}
        {onCancel && (
          <Button
            type="button"
            variant="ghost"
            onClick={onCancel}
            isDisabled={isSubmitting}
          >
            {cancelLabel}
          </Button>
        )}
        <Button
          type="submit"
          variant="primary"
          isLoading={isSubmitting}
          isDisabled={!isDirty || isLocked}
        >
          {submitLabel}
        </Button>
      </div>
    </div>
  );
}
