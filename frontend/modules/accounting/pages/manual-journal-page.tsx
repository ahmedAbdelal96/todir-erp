import React, { useState } from "react";
import { DocumentPage, type DocumentStatusGroup } from "../../../components/pages/document-page.tsx";
import { DataGrid, type DataGridColumn } from "../../../components/grid/index.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { useJournalRecord, usePostJournal, useValidateJournal } from "../hooks/journal-queries.ts";
import type { JournalLine, JournalEntry } from "../types.ts";

export interface ManualJournalPageProps {
  readonly journalId?: string | undefined;
  readonly onBack?: (() => void) | undefined;
}

export function ManualJournalPage({
  journalId = "jrnl-001",
  onBack
}: ManualJournalPageProps): React.JSX.Element {
  const { data: initialJournal, isLoading, error } = useJournalRecord(journalId);
  const postJournalMutation = usePostJournal();
  const validateJournalMutation = useValidateJournal();

  const [lines, setLines] = useState<JournalLine[]>([]);
  const [isInitialized, setIsInitialized] = useState(false);

  if (initialJournal && !isInitialized) {
    setLines([...initialJournal.lines]);
    setIsInitialized(true);
  }

  if (isLoading) {
    return <div style={{ padding: "2rem", textAlign: "center" }}>جارٍ تحميل قيد اليومية...</div>;
  }

  if (error || !initialJournal) {
    return (
      <div style={{ padding: "2rem", textAlign: "center" }}>
        <p>تعذر تحميل بيانات قيد اليومية أو أن القيد غير موجود.</p>
        {onBack && <Button onPress={onBack}>العودة</Button>}
      </div>
    );
  }

  const isLocked = initialJournal.status === "Posted" || initialJournal.isPeriodClosed;

  const handleUpdateLine = (index: number, updates: Partial<JournalLine>) => {
    if (isLocked) return;
    const next = [...lines];
    const target = next[index];
    if (!target) return;
    next[index] = { ...target, ...updates };
    setLines(next);
  };

  const handleAddLine = () => {
    if (isLocked) return;
    const newLine: JournalLine = {
      id: `line-${Date.now()}`,
      accountCode: "1010",
      accountName: "البنك الأهلي التجاري",
      description: "",
      debit: "0.00",
      credit: "0.00"
    };
    setLines([...lines, newLine]);
  };

  const handleRemoveLine = (index: number) => {
    if (isLocked || lines.length <= 2) return;
    setLines(lines.filter((_, i) => i !== index));
  };

  const handlePost = async () => {
    if (isLocked) return;
    await postJournalMutation.mutateAsync({ id: initialJournal.id });
  };

  const columns: DataGridColumn<JournalLine>[] = [
    {
      id: "accountCode",
      header: "رمز الحساب والاسم",
      width: 220,
      cellRenderer: ({ row, value }) => (
        <span style={{ fontWeight: 600 }}>
          {row.accountCode} — {row.accountName}
        </span>
      )
    },
    {
      id: "description",
      header: "البيان / الشرح",
      width: "1fr",
      cellRenderer: ({ row }) => (
        <span>{row.description || "—"}</span>
      )
    },
    {
      id: "debit",
      header: "مدين (Debit)",
      width: 150,
      align: "end",
      cellRenderer: ({ row }) => (
        <span style={{ fontWeight: 600, color: row.debit !== "0.00" ? "var(--color-brand-primary)" : undefined }}>
          <MoneyDisplay value={{ amount: row.debit, currency: "SAR" }} />
        </span>
      )
    },
    {
      id: "credit",
      header: "دائن (Credit)",
      width: 150,
      align: "end",
      cellRenderer: ({ row }) => (
        <span style={{ fontWeight: 600, color: row.credit !== "0.00" ? "var(--color-status-danger)" : undefined }}>
          <MoneyDisplay value={{ amount: row.credit, currency: "SAR" }} />
        </span>
      )
    },
    {
      id: "costCenter",
      header: "مركز التكلفة",
      width: 140,
      cellRenderer: ({ row }) => <span>{row.costCenter ?? "—"}</span>
    }
  ];

  const statuses: DocumentStatusGroup = {
    lifecycle: initialJournal.status === "Posted" ? "posted" : "draft",
    accounting: initialJournal.status === "Posted" ? "Posted" : "Unposted",
    isLocked,
    lockedReason: initialJournal.status === "Posted" ? "القيد مرحل إلى دفتر الأستاذ العام" : initialJournal.isPeriodClosed ? "الفترة المالية مغلقة" : undefined
  };

  return (
    <DocumentPage
      documentType="قيد يومية يدوي (Manual Journal)"
      documentNumber={initialJournal.entryNumber}
      statuses={statuses}
      breadcrumbs={[
        { label: "المحاسبة العامة", href: "/accounting" },
        { label: "قيود اليومية", href: "/accounting", onClick: onBack },
        { label: initialJournal.entryNumber }
      ]}
      headerActions={
        <>
          {onBack && <Button variant="secondary" onPress={onBack}>العودة</Button>}
          {!isLocked && (
            <Button
              variant="primary"
              onPress={handlePost}
              isDisabled={!initialJournal.isBalanced || postJournalMutation.isPending}
            >
              {postJournalMutation.isPending ? "جارٍ الترحيل..." : "ترحيل القيد"}
            </Button>
          )}
        </>
      }
      metadataSection={
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
          <div>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>تاريخ القيد</div>
            <div style={{ fontWeight: 600 }}>{initialJournal.date}</div>
          </div>
          <div>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>الفترة المحاسبية</div>
            <div style={{ fontWeight: 600 }}>{initialJournal.period}</div>
          </div>
          <div>
            <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>حالة التوازن</div>
            <div style={{ fontWeight: 600, color: initialJournal.isBalanced ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
              {initialJournal.isBalanced ? "✓ القيد متوازن" : "⚠️ القيد غير متوازن"}
            </div>
          </div>
        </div>
      }
      linesSection={
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <div style={{ minHeight: "260px" }}>
            <DataGrid<JournalLine>
              columns={columns}
              staticRows={lines}
              density="compact"
              selectable="none"
            />
          </div>
          {!isLocked && (
            <div style={{ padding: "0.5rem 1rem" }}>
              <Button variant="secondary" size="compact" onPress={handleAddLine}>
                + إضافة سطر محاسبي جديد
              </Button>
            </div>
          )}
        </div>
      }
      summaryItems={[
        {
          label: "إجمالي المدين (Debit)",
          value: <MoneyDisplay value={{ amount: initialJournal.debitTotal, currency: "SAR" }} />
        },
        {
          label: "إجمالي الدائن (Credit)",
          value: <MoneyDisplay value={{ amount: initialJournal.creditTotal, currency: "SAR" }} />
        },
        {
          label: "الفارق المحاسبي",
          value: <MoneyDisplay value={{ amount: "0.00", currency: "SAR" }} />,
          isEmphasized: true
        }
      ]}
      notesSection={
        <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", backgroundColor: "var(--color-surface-subtle)", padding: "1rem", borderRadius: "var(--radius-md)" }}>
          <div style={{ fontWeight: 600, marginBottom: "0.25rem" }}>البيان العام للقيد</div>
          <div>{initialJournal.description}</div>
        </div>
      }
    />
  );
}
