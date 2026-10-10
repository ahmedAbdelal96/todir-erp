import type { DecimalString, CurrencyCode } from "../../components/contracts/domain-values.ts";

export type PaymentMethod = "BankTransfer" | "Cash" | "Check" | "Card";
export type PaymentStatus = "Settled" | "Processing" | "Rejected" | "Unknown";

export interface PaymentCommand {
  readonly partyId: string;
  readonly partyName: string;
  readonly amount: DecimalString;
  readonly currency: CurrencyCode;
  readonly paymentMethod: PaymentMethod;
  readonly referenceNumber: string;
  readonly sourceAccount: string;
  readonly destinationAccount: string;
  readonly notes?: string | undefined;
}

export interface PaymentResult {
  readonly paymentId: string;
  readonly referenceNumber: string;
  readonly status: PaymentStatus;
  readonly amount: DecimalString;
  readonly currency: CurrencyCode;
  readonly settledAt?: string | undefined;
  readonly message?: string | undefined;
}

export interface PaymentStatusCheck {
  readonly paymentId: string;
  readonly status: PaymentStatus;
  readonly settledAt?: string | undefined;
}
