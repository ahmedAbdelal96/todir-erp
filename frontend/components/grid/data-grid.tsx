import React, { useMemo } from "react";
import { AgGridReact } from "ag-grid-react";
import { AllCommunityModule, ModuleRegistry } from "ag-grid-community";
import type { ColDef } from "ag-grid-community";
import { resolveDataGridLocale, type DataGridProps, type DataGridColumn } from "./grid-contract.ts";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

// Register the free Community feature set once at the design-system boundary.
// Business modules consume DataGrid without depending on AG Grid internals.
ModuleRegistry.registerModules([AllCommunityModule]);

export function DataGrid<TRow extends object>({
  columns,
  staticRows = [],
  density = "comfortable",
  locale,
  enableRtl,
  selectable = "none",
  onSelectionChange,
  loading = false,
  emptyMessage,
  hasActiveFilters = false,
  loadingMessage
}: DataGridProps<TRow>): React.JSX.Element {
  const { t, i18n } = useTranslation("common");
  const resolvedLocale = resolveDataGridLocale(locale);
  const resolvedEnableRtl = enableRtl ?? i18n.dir(resolvedLocale) === "rtl";
  const resolvedEmptyMessage = emptyMessage ?? t(hasActiveFilters ? "components.noRowsFiltered" : "components.noRows", { lng: resolvedLocale });
  const resolvedLoadingMessage = loadingMessage ?? t("components.loadingRecords", { lng: resolvedLocale });
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
      className={clsx("ui-datagrid", `ui-datagrid--${density}`, { "ag-rtl": resolvedEnableRtl })}
      style={{
        height: "480px",
        width: "100%",
        backgroundColor: "var(--color-surface-card)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        border: "1px solid var(--color-border-default)",
        overflow: "hidden"
      }}
    >
      <AgGridReact<TRow>
        key={resolvedEnableRtl ? "rtl" : "ltr"}
        rowData={staticRows as TRow[]}
        columnDefs={colDefs}
        rowHeight={rowHeight}
        headerHeight={headerHeight}
        enableRtl={resolvedEnableRtl}
        localeText={{
          selectAll: t("grid.selectAll", { lng: resolvedLocale }), searchOoo: t("grid.searchOoo", { lng: resolvedLocale }),
          blanks: t("grid.blanks", { lng: resolvedLocale }), noMatches: t("grid.noMatches", { lng: resolvedLocale }),
          filterOoo: t("grid.filterOoo", { lng: resolvedLocale }), equals: t("grid.equals", { lng: resolvedLocale }),
          notEqual: t("grid.notEqual", { lng: resolvedLocale }), lessThan: t("grid.lessThan", { lng: resolvedLocale }),
          greaterThan: t("grid.greaterThan", { lng: resolvedLocale }), lessThanOrEqual: t("grid.lessThanOrEqual", { lng: resolvedLocale }),
          greaterThanOrEqual: t("grid.greaterThanOrEqual", { lng: resolvedLocale }), inRange: t("grid.inRange", { lng: resolvedLocale }),
          contains: t("grid.contains", { lng: resolvedLocale }), notContains: t("grid.notContains", { lng: resolvedLocale }),
          startsWith: t("grid.startsWith", { lng: resolvedLocale }), endsWith: t("grid.endsWith", { lng: resolvedLocale }),
          noRowsToShow: resolvedEmptyMessage, loadingOoo: resolvedLoadingMessage,
          page: t("grid.page", { lng: resolvedLocale }), of: t("grid.of", { lng: resolvedLocale }), to: t("grid.to", { lng: resolvedLocale }),
          next: t("grid.next", { lng: resolvedLocale }), previous: t("grid.previous", { lng: resolvedLocale })
        }}
        autoSizeStrategy={{ type: "fitGridWidth", defaultMinWidth: 80 }}
        onGridReady={(event) => {
          event.api.sizeColumnsToFit();
        }}
        onGridSizeChanged={(event) => {
          event.api.sizeColumnsToFit();
        }}
        {...(selectable !== "none" ? { rowSelection: selectable === "multi" ? "multiple" : "single" } : {})}
        loading={loading}
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
        overlayNoRowsTemplate={`<span class="ui-datagrid__empty">${resolvedEmptyMessage}</span>`}
        overlayLoadingTemplate={`<span class="ui-datagrid__loading">${resolvedLoadingMessage}</span>`}
        animateRows={false}
      />
    </div>
  );
}
