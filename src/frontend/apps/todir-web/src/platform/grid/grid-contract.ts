import type { ReactNode } from "react";
import type { DensityMode } from "../runtime/preferences.ts";

export type ColumnAlign = "start" | "center" | "end";

export interface DataGridColumn<TRow> {
  readonly id: string;
  readonly header: string;
  readonly width?: number | string | undefined;
  readonly minWidth?: number | undefined;
  readonly maxWidth?: number | undefined;
  readonly align?: ColumnAlign | undefined;
  readonly sortable?: boolean | undefined;
  readonly filterable?: boolean | undefined;
  readonly resizable?: boolean | undefined;
  readonly isSensitive?: boolean | undefined;
  readonly requiredPermission?: string | undefined;
  readonly cellRenderer?: (params: { row: TRow; value: unknown }) => ReactNode;
}

export interface DataGridSortModel {
  readonly columnId: string;
  readonly direction: "asc" | "desc";
}

export interface DataGridFilterModel {
  readonly [columnId: string]: {
    readonly filterType: "text" | "number" | "date" | "set";
    readonly value: unknown;
  };
}

export interface DataGridServerDataSource<TRow> {
  readonly fetchRows: (params: {
    readonly startRow: number;
    readonly endRow: number;
    readonly sortModel?: readonly DataGridSortModel[] | undefined;
    readonly filterModel?: DataGridFilterModel | undefined;
  }) => Promise<{
    readonly rows: readonly TRow[];
    readonly totalCount: number;
  }>;
}

export interface DataGridProps<TRow> {
  readonly columns: readonly DataGridColumn<TRow>[];
  readonly dataSource?: DataGridServerDataSource<TRow> | undefined;
  readonly staticRows?: readonly TRow[] | undefined;
  readonly density?: DensityMode | undefined;
  readonly enableRtl?: boolean | undefined;
  readonly selectable?: "none" | "single" | "multi" | undefined;
  readonly onSelectionChange?: (selectedRows: readonly TRow[]) => void;
  readonly emptyMessage?: string | undefined;
  readonly loadingMessage?: string | undefined;
}
