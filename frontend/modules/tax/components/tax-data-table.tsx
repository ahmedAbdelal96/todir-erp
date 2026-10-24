import React, { type ReactNode } from "react";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";

export interface TaxTableColumn<T> {
  readonly key: string;
  readonly header: ReactNode;
  readonly width?: string | undefined;
  readonly align?: "start" | "center" | "end" | undefined;
  readonly render: (item: T, index: number) => ReactNode;
}

export interface TaxDataTableProps<T> {
  readonly data: readonly T[];
  readonly columns: readonly TaxTableColumn<T>[];
  readonly rowKey: (item: T, index: number) => string;
}

export function TaxDataTable<T>({
  data,
  columns,
  rowKey
}: TaxDataTableProps<T>): React.JSX.Element {
  return (
    <div
      style={{
        width: "100%",
        maxWidth: "100%",
        overflowX: "auto",
        WebkitOverflowScrolling: "touch"
      }}
    >
      <Table density="comfortable" isBordered>
      <TableHeader>
        <TableRow>
          {columns.map((col) => (
            <TableHead
              key={col.key}
              align={col.align ?? "start"}
              style={col.width ? { width: col.width } : undefined}
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
                style={col.width ? { width: col.width } : undefined}
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
