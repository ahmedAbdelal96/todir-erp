import React, { type ReactNode, type HTMLAttributes, type TdHTMLAttributes, type ThHTMLAttributes } from "react";
import clsx from "clsx";

export interface TableProps extends HTMLAttributes<HTMLTableElement> {
  readonly density?: "comfortable" | "compact" | undefined;
  readonly isStriped?: boolean | undefined;
  readonly isBordered?: boolean | undefined;
  readonly children: ReactNode;
}

export function Table({
  density = "comfortable",
  isStriped = false,
  isBordered = true,
  className,
  children,
  ...props
}: TableProps): React.JSX.Element {
  return (
    <div
      className="ui-table-container"
      style={{
        width: "100%",
        overflowX: "auto",
        border: isBordered ? "1px solid var(--color-border-default)" : "none",
        borderRadius: "var(--radius-lg, 0.75rem)",
        backgroundColor: "var(--color-surface-card)"
      }}
    >
      <table
        className={clsx("ui-table", `ui-table--${density}`, { "ui-table--striped": isStriped }, className)}
        style={{
          width: "100%",
          tableLayout: "fixed",
          borderCollapse: "collapse",
          fontSize: density === "compact" ? "0.8125rem" : "0.875rem",
          textAlign: "start",
          color: "var(--color-content-primary)"
        }}
        {...props}
      >
        {children}
      </table>
    </div>
  );
}

export function TableHeader({ className, children, ...props }: HTMLAttributes<HTMLTableSectionElement>): React.JSX.Element {
  return (
    <thead
      className={clsx("ui-table-header", className)}
      style={{
        backgroundColor: "var(--color-surface-subtle)",
        borderBottom: "1px solid var(--color-border-default)"
      }}
      {...props}
    >
      {children}
    </thead>
  );
}

export function TableBody({ className, children, ...props }: HTMLAttributes<HTMLTableSectionElement>): React.JSX.Element {
  return (
    <tbody className={clsx("ui-table-body", className)} {...props}>
      {children}
    </tbody>
  );
}

export function TableFooter({ className, children, ...props }: HTMLAttributes<HTMLTableSectionElement>): React.JSX.Element {
  return (
    <tfoot
      className={clsx("ui-table-footer", className)}
      style={{
        borderTop: "2px solid var(--color-border-default)",
        fontWeight: 600,
        backgroundColor: "var(--color-surface-subtle)"
      }}
      {...props}
    >
      {children}
    </tfoot>
  );
}

export interface TableRowProps extends HTMLAttributes<HTMLTableRowElement> {
  readonly isSelected?: boolean | undefined;
}

export function TableRow({ className, isSelected, children, ...props }: TableRowProps): React.JSX.Element {
  return (
    <tr
      className={clsx("ui-table-row", { "is-selected": isSelected }, className)}
      style={{
        borderBottom: "1px solid var(--color-border-default)",
        backgroundColor: isSelected ? "var(--color-surface-primary-subtle, #eff6ff)" : undefined
      }}
      {...props}
    >
      {children}
    </tr>
  );
}

export interface TableHeadProps extends Omit<ThHTMLAttributes<HTMLTableCellElement>, "align"> {
  readonly align?: "start" | "center" | "end" | undefined;
}

export function TableHead({ align = "start", className, children, ...props }: TableHeadProps): React.JSX.Element {
  return (
    <th
      scope="col"
      className={clsx("ui-table-head", className)}
      style={{
        padding: "0.875rem 1rem",
        textAlign: align,
        fontWeight: 700,
        fontSize: "0.75rem",
        letterSpacing: "0.04em",
        color: "var(--color-content-muted, #64748b)",
        overflowWrap: "anywhere",
        wordBreak: "break-word"
      }}
      {...props}
    >
      {children}
    </th>
  );
}

export interface TableCellProps extends Omit<TdHTMLAttributes<HTMLTableCellElement>, "align"> {
  readonly align?: "start" | "center" | "end" | undefined;
}

export function TableCell({ align = "start", className, children, ...props }: TableCellProps): React.JSX.Element {
  return (
    <td
      className={clsx("ui-table-cell", className)}
      style={{
        padding: "0.875rem 1rem",
        textAlign: align,
        verticalAlign: "middle",
        overflowWrap: "anywhere",
        wordBreak: "break-word"
      }}
      {...props}
    >
      {children}
    </td>
  );
}
