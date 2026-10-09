import React from 'react';
import { DataGrid } from './data-grid.tsx';
import type { DataGridColumn } from './grid-contract.ts';

export default {
  title: 'DataGrid/ProductionWrapper',
};

interface RowData {
  id: string;
  code: string;
  name: string;
  balance: string;
  status: string;
}

const rowData: RowData[] = [
  { id: '1', code: 'CUST-001', name: 'شركة الأفق للتجارة والمقاولات', balance: '124,500.00 SAR', status: 'نشط' },
  { id: '2', code: 'CUST-002', name: 'مؤسسة الرياض للحلول الرقمية', balance: '45,200.00 SAR', status: 'نشط' },
  { id: '3', code: 'CUST-003', name: 'شركة النيل للصناعات الهندسية', balance: '0.00 SAR', status: 'معلق' },
  { id: '4', code: 'CUST-004', name: 'مكتب الرواد للاستشارات', balance: '18,750.50 SAR', status: 'نشط' },
];

const columns: DataGridColumn<RowData>[] = [
  { id: 'code', header: 'الرمز', width: 120 },
  { id: 'name', header: 'اسم الطرف التجاري', width: 'flex' },
  { id: 'balance', header: 'الرصيد القائم', width: 'flex', align: 'end' },
  { id: 'status', header: 'الحالة', width: 100 },
];

export const DefaultGrid = () => {
  return (
    <div style={{ height: '350px', width: '100%' }}>
      <DataGrid
        columns={columns}
        staticRows={rowData}
        selectable="multi"
        onSelectionChange={(selected: readonly RowData[]) => console.log('Selected:', selected)}
      />
    </div>
  );
};
