import type { DecimalString, CurrencyCode } from "../../design-system/contracts/domain-values.ts";

export type JournalStatus = "Draft" | "Posted" | "Reversed";
export type AccountType = "Asset" | "Liability" | "Equity" | "Revenue" | "Expense";

export interface Account {
  readonly code: string;
  readonly name: string;
  readonly type: AccountType;
  readonly currency: CurrencyCode;
}

export interface JournalLine {
  readonly id: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly description: string;
  readonly debit: DecimalString;
  readonly credit: DecimalString;
  readonly costCenter?: string | undefined;
  readonly reference?: string | undefined;
}

export interface JournalEntry {
  readonly id: string;
  readonly entryNumber: string;
  readonly date: string;
  readonly description: string;
  readonly status: JournalStatus;
  readonly period: string;
  readonly isPeriodClosed: boolean;
  readonly lines: readonly JournalLine[];
  readonly debitTotal: DecimalString;
  readonly creditTotal: DecimalString;
  readonly isBalanced: boolean;
  readonly postedAt?: string | undefined;
  readonly postedBy?: string | undefined;
}

export interface JournalValidationResult {
  readonly isValid: boolean;
  readonly isBalanced: boolean;
  readonly debitTotal: DecimalString;
  readonly creditTotal: DecimalString;
  readonly difference: DecimalString;
  readonly lineErrors?: readonly {
    readonly lineIndex: number;
    readonly field: string;
    readonly message: string;
  }[] | undefined;
}
