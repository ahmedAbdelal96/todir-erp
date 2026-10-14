export const en = {
  title: "Payments and receipts", subtitle: "Manage financial transactions and bank transfers", newPayment: "Record a new payment",
  entry: {
    invalidAmount: "Enter a valid payment amount greater than zero.", submitFailed: "The payment could not be submitted.", confirmTitle: "Confirm outgoing payment",
    unknownOutcomeTitle: "Transfer status is uncertain", successTitle: "Payment completed", title: "Record an outgoing payment",
    confirmDescription: "Review the payment details before final submission.", description: "Payment for: {{name}}", amount: "Amount",
    paymentMethod: "Payment method", bankTransfer: "Bank transfer", cash: "Cash", check: "Bank check", card: "Payment card",
    sourceAccount: "Source account / cashbox", destinationAccount: "Destination account / payee", referenceNumber: "Reference number", notes: "Notes and description",
    cancel: "Cancel", continueToConfirmation: "Continue to confirmation", payee: "Payee:", totalAmount: "Total amount:",
    sourceAccountLabel: "Source account:", referenceLabel: "Reference number:", irreversibleWarning: "This payment instruction will be submitted as a final action and cannot be automatically reversed.",
    editDetails: "Edit details", submitting: "Processing transfer...", confirmAndSend: "Confirm and send payment", connectionInterrupted: "The connection was interrupted while processing the payment:",
    unknownOutcomeDetails: "The bank transfer may already have been completed by the provider. To avoid a duplicate debit, do not submit again. Check the payment status in the accounts screen.",
    closeAndCheckStatus: "Close and check status", successMessage: "Payment recorded successfully", paymentReference: "Transaction reference", done: "Done"
  }
} as const;
