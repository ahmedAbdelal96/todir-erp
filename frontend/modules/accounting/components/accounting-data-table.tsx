import React, { type ReactNode } from "react";
import clsx from "clsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";

export interface AccountingTableColumn<T> {
  readonly key: string;
  readonly header: ReactNode;
  readonly width?: string | undefined;
  readonly minWidth?: string | undefined;
  readonly align?: "start" | "center" | "end" | undefined;
  readonly render: (item: T, index: number) => ReactNode;
}

export interface AccountingDataTableProps<T> {
  readonly data: readonly T[];
  readonly columns: readonly AccountingTableColumn<T>[];
  readonly rowKey: (item: T, index: number) => string;
  readonly emptyMessage?: ReactNode | undefined;
  readonly minWidth?: string | undefined;
  readonly className?: string | undefined;
}

export function AccountingDataTable<T>({
  data,
  columns,
  rowKey,
  emptyMessage,
  minWidth = "1050px",
  className
}: AccountingDataTableProps<T>): React.JSX.Element {
  if (data.length === 0 && emptyMessage) {
    return (
      <div
        style={{
          padding: "2rem",
          textAlign: "center",
          color: "var(--color-content-muted)",
          backgroundColor: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          border: "1px dashed var(--color-border-default)"
        }}
      >
        {emptyMessage}
      </div>
    );
  }

  return (
    <div
      className={clsx("accounting-table-container", className)}
      style={{
        width: "100%",
        maxWidth: "100%",
        overflowX: "auto",
        WebkitOverflowScrolling: "touch"
      }}
    >
      <Table
        density="comfortable"
        isBordered
        style={{
          minWidth,
          tableLayout: "fixed"
        }}
      >
        <TableHeader>
          <TableRow>
            {columns.map((col) => (
              <TableHead
                key={col.key}
                align={col.align ?? "start"}
                style={{
                  ...(col.width ? { width: col.width } : {}),
                  ...(col.minWidth ? { minWidth: col.minWidth } : {})
                }}
              >
                {col.header}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.map((item, index) => (
            <TableRow key={rowKey(item, index)}>
              {columns.map((col) => (
                <TableCell
                  key={col.key}
                  align={col.align ?? "start"}
                  style={{
                    ...(col.width ? { width: col.width } : {}),
                    ...(col.minWidth ? { minWidth: col.minWidth } : {})
                  }}
                >
                  {col.render(item, index)}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
