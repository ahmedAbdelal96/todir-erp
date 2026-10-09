import React, { useState } from "react";
import { Pagination } from "./pagination.tsx";
import { ConfirmDialog } from "./confirm-dialog.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell, TableFooter } from "./table.tsx";
import { FileInput } from "./file-input.tsx";
import { Skeleton } from "./skeleton.tsx";
import { SearchField } from "./search-field.tsx";
import { Button } from "./button.tsx";
import { ToastProvider, useToast } from "./toast.tsx";
import { Alert } from "./alert.tsx";
import { ProgressBar } from "./progress-bar.tsx";
import { FilterBar } from "./filter-bar.tsx";

export default {
  title: "Primitives/Extended Primitives",
};

export const PaginationStory = () => {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", padding: "1rem" }}>
      <h3>مكون التنقل بين الصفحات (Pagination)</h3>
      <Pagination
        page={page}
        totalPages={10}
        totalItems={195}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  );
};

export const ConfirmDialogStory = () => {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div style={{ padding: "1rem" }}>
      <h3>نافذة تأكيد الإجراءات الخطرة (ConfirmDialog)</h3>
      <Button variant="danger" onPress={() => setIsOpen(true)}>
        حذف المستند نهائياً
      </Button>
      <ConfirmDialog
        isOpen={isOpen}
        onOpenChange={setIsOpen}
        title="تأكيد حذف المستند"
        description="هل أنت متأكد من رغبتك في حذف هذا المستند؟ هذا الإجراء نهائي ولا يمكن التراجع عنه."
        confirmLabel="نعم، احذف المستند"
        cancelLabel="تراجع"
        variant="danger"
        onConfirm={() => setIsOpen(false)}
      />
    </div>
  );
};

export const TableStory = () => {
  return (
    <div style={{ padding: "1rem" }}>
      <h3>الجدول البسيط للقوائم المحدودة (Simple Table)</h3>
      <Table density="comfortable" isStriped>
        <TableHeader>
          <TableRow>
            <TableHead>#</TableHead>
            <TableHead>البند / الحساب</TableHead>
            <TableHead align="end">المبلغ (SAR)</TableHead>
            <TableHead>الحالة</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell>1</TableCell>
            <TableCell>1010 - البنك الأهلي التجاري</TableCell>
            <TableCell align="end">25,400.00</TableCell>
            <TableCell>نشط</TableCell>
          </TableRow>
          <TableRow>
            <TableCell>2</TableCell>
            <TableCell>1020 - صندوق النقدية الرئيسي</TableCell>
            <TableCell align="end">5,000.00</TableCell>
            <TableCell>نشط</TableCell>
          </TableRow>
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell colSpan={2}>الإجمالي</TableCell>
            <TableCell align="end">30,400.00</TableCell>
            <TableCell>—</TableCell>
          </TableRow>
        </TableFooter>
      </Table>
    </div>
  );
};

export const FileInputStory = () => {
  const [files, setFiles] = useState<File[]>([]);

  return (
    <div style={{ padding: "1rem", maxWidth: "480px" }}>
      <h3>حقل رفع وسحب الملفات (FileInput)</h3>
      <FileInput
        label="المرفقات والمستندات المؤيدة"
        description="يرجى إرفاق صورة العقد أو الفاتورة الضريبية بصيغة PDF أو PNG."
        accept=".pdf,.png,.jpg"
        maxSizeBytes={5 * 1024 * 1024}
        files={files}
        onFilesChange={setFiles}
      />
    </div>
  );
};

export const SkeletonStory = () => {
  return (
    <div style={{ padding: "1rem", display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "400px" }}>
      <h3>عناصر التحميل المؤقت (Skeleton Loading)</h3>
      <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
        <Skeleton variant="circular" width="48px" height="48px" />
        <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <Skeleton variant="text" width="60%" height="1.2em" />
          <Skeleton variant="text" width="40%" height="0.9em" />
        </div>
      </div>
      <Skeleton variant="rectangular" height="120px" />
    </div>
  );
};

export const SearchFieldStory = () => {
  const [searchVal, setSearchVal] = useState("");

  return (
    <div style={{ padding: "1rem" }}>
      <h3>حقل البحث السريع (SearchField)</h3>
      <SearchField
        value={searchVal}
        onChange={setSearchVal}
        placeholder="ابحث بالاسم أو الرقم المرجعي..."
      />
      <div style={{ marginTop: "0.5rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
        القيمة الحالية: {searchVal || "(فارغ)"}
      </div>
    </div>
  );
};

function ToastTriggerDemo() {
  const { showToast } = useToast();

  return (
    <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
      <Button
        variant="primary"
        onPress={() =>
          showToast({
            type: "success",
            title: "تم حفظ المستند بنجاح",
            description: "تم ترحيل الفاتورة الضريبية برقم INV-2026-0091."
          })
        }
      >
        إشعار نجاح (Success Toast)
      </Button>
      <Button
        variant="danger"
        onPress={() =>
          showToast({
            type: "error",
            title: "فشل ترحيل القيد المحاسبي",
            description: "القيد غير متوازن: إجمالي المدين لا يساوي إجمالي الدائن."
          })
        }
      >
        إشعار خطأ (Error Toast)
      </Button>
    </div>
  );
}

export const ToastStory = () => {
  return (
    <ToastProvider>
      <div style={{ padding: "1rem" }}>
        <h3>نظام الإشعارات اللحظية (Toast System)</h3>
        <ToastTriggerDemo />
      </div>
    </ToastProvider>
  );
};

export const AlertStory = () => {
  return (
    <div style={{ padding: "1rem", display: "flex", flexDirection: "column", gap: "1rem", maxWidth: "600px" }}>
      <h3>التنبيهات الثابتة (Persistent Alerts / Banners)</h3>
      <Alert variant="info" title="معلومة تشغيلية">
        تم تحديث أسعار الصرف تلقائياً وفقاً للبنك المركزي السعودي.
      </Alert>
      <Alert variant="warning" title="تنبيه: فترة محاسبية مغلقة" isDismissible>
        الفترة المحاسبية لشهر سبتمبر مغلقة. التعديل يتطلب صلاحية إدارية خاصة.
      </Alert>
      <Alert variant="danger" title="خطأ ترحيل غير قابل للتجاوز">
        لا يمكن ترحيل الفاتورة: الرقم الضريبي للعميل غير مسجل.
      </Alert>
      <Alert variant="success" title="عملية مكتملة">
        تم استيراد كافة السجلات بنجاح وتحديث دليل الأطراف.
      </Alert>
    </div>
  );
};

export const ProgressBarStory = () => {
  return (
    <div style={{ padding: "1rem", display: "flex", flexDirection: "column", gap: "1.5rem", maxWidth: "480px" }}>
      <h3>مؤشرات التقدم (Progress Bars)</h3>
      <ProgressBar label="جارٍ رفع ملف الفواتير..." value={65} />
      <ProgressBar label="التقدم المكتمل" value={100} variant="success" />
      <ProgressBar label="معالجة البيانات في الخلفية (غير محدد)..." isIndeterminate />
    </div>
  );
};

export const FilterBarStory = () => {
  return (
    <div style={{ padding: "1rem" }}>
      <h3>شريط الفلاتر والبحث (FilterBar)</h3>
      <FilterBar
        searchInput={<SearchField placeholder="ابحث في السجلات..." />}
        activeFilterCount={2}
        onResetFilters={() => {}}
      >
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <select style={{ padding: "0.375rem 0.75rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}>
            <option>جميع الفئات</option>
            <option>عملاء</option>
            <option>موردون</option>
          </select>
        </div>
      </FilterBar>
    </div>
  );
};
