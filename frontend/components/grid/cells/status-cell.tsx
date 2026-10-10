import React from "react";
import { StatusBadge, type StatusVariant } from "../../fields/status-badge.tsx";

export interface StatusCellProps {
  readonly status: StatusVariant | string | null | undefined;
  readonly label?: string | undefined;
}

export function StatusCell({ status, label }: StatusCellProps): React.JSX.Element {
  if (!status) return <span>—</span>;
  return <StatusBadge status={status} label={label} size="small" />;
}
