import React, { useMemo } from "react";
import { AgGridReact } from "ag-grid-react";
import { AllCommunityModule, ModuleRegistry } from "ag-grid-community";
import type { ColDef } from "ag-grid-community";
import type { DataGridProps, DataGridColumn } from "./grid-contract.ts";
import clsx from "clsx";

// Register the free Community feature set once at the design-system boundary.
// Business modules consume DataGrid without depending on AG Grid internals.
ModuleRegistry.registerModules([AllCommunityModule]);

export function DataGrid<TRow extends object>({
  columns,
  staticRows = [],
  density = "comfortable",
  enableRtl = false,
  selectable = "none",
  onSelectionChange,
  emptyMessage = "No records found",
  loadingMessage = "Loading records..."
}: DataGridProps<TRow>): React.JSX.Element {
  const colDefs = useMemo<ColDef<TRow>[]>(() => {
    return columns.map((col: DataGridColumn<TRow>) => {
      const colDef: ColDef<TRow> = {
        colId: col.id,
        headerName: col.header,
        field: col.id as any,
        // Columns may expose exact strings, decimal strings, or arrays to a
        // custom renderer. Do not let AG Grid infer a formatter from the raw
        // value and emit misleading type warnings for domain fields.
        cellDataType: false,
        sortable: col.sortable ?? true,
        filter: col.filterable ?? true,
        resizable: col.resizable ?? true,
        ...(col.minWidth !== undefined ? { minWidth: col.minWidth } : {}),
        ...(col.maxWidth !== undefined ? { maxWidth: col.maxWidth } : {})
      };

      if (typeof col.width === "number") {
        colDef.width = col.width;
      } else if (typeof col.width === "string") {
        colDef.flex = 1;
      }

      if (col.align) {
        colDef.cellStyle = {
          textAlign: col.align === "start" ? "start" : col.align === "end" ? "end" : "center"
        };
      }

      if (col.cellRenderer) {
        const renderer = col.cellRenderer;
        colDef.cellRenderer = (params: any) => {
          if (!params.data) return null;
          return renderer({ row: params.data, value: params.value });
        };
      }

      return colDef;
    });
  }, [columns]);

  const rowHeight = density === "compact" ? 36 : 48;
  const headerHeight = density === "compact" ? 38 : 50;

  return (
    <div
      className={clsx("ui-datagrid", `ui-datagrid--${density}`, { "ag-rtl": enableRtl })}
      style={{
        height: "480px",
        width: "100%",
        backgroundColor: "var(--color-surface-card)",
        borderRadius: "var(--radius-md)",
        border: "1px solid var(--color-border-default)",
        overflow: "hidden"
      }}
    >
      <AgGridReact<TRow>
        rowData={staticRows as TRow[]}
        columnDefs={colDefs}
        rowHeight={rowHeight}
        headerHeight={headerHeight}
        enableRtl={enableRtl}
        {...(selectable !== "none" ? { rowSelection: selectable === "multi" ? "multiple" : "single" } : {})}
        onRowClicked={(event) => {
          if (event.data && onSelectionChange) {
            onSelectionChange([event.data]);
          }
        }}
        onSelectionChanged={(event) => {
          if (onSelectionChange) {
            const selectedRows = event.api.getSelectedRows();
            onSelectionChange(selectedRows);
          }
        }}
        overlayNoRowsTemplate={`<span class="ui-datagrid__empty">${emptyMessage}</span>`}
        overlayLoadingTemplate={`<span class="ui-datagrid__loading">${loadingMessage}</span>`}
        animateRows={false}
      />
    </div>
  );
}
