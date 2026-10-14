export const en = {
  title: "Sales invoices",
  document: {
    loading: "Loading invoice details...", loadError: "Could not load the invoice, or the document does not exist.", back: "Back",
    concurrentEdit: "Another user changed this document. Reload the page to review the latest details.", postError: "Could not post the invoice to the accounts.",
    postedLocked: "The document has been posted and recorded in the ledger", subtotal: "Subtotal (before discount and tax)", totalDiscount: "Total trade discount",
    totalTax: "Value added tax (15%)", totalDue: "Total amount due", type: "Tax sales invoice", sales: "Sales",
    issueCreditNote: "Issue credit note", posting: "Posting...", postToAccounts: "Post to accounts", customer: "Customer / business party",
    issueDate: "Issue date", dueDate: "Due date", currency: "Currency", lineDescription: "Description / item", quantity: "Quantity",
    unit: "Unit", unitPrice: "Unit price", discount: "Discount", tax: "Tax", total: "Total", notesAndTerms: "Notes and terms",
    creditNoteTitle: "Issue a credit note", creditNoteDescription: "Correct or cancel posted invoice {{number}}",
    creditNoteWarning: "Posted invoices cannot be deleted or edited under financial compliance and journal rules. A linked credit note will be created to reverse the accounting entries.",
    cancel: "Cancel", continueCreditNote: "Continue to credit note"
  }
} as const;
