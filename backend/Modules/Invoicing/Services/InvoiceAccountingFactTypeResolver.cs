using Modules.Invoicing.Entities;
using Shared.Contracts.Ledger;

namespace Modules.Invoicing.Services;

public static class InvoiceAccountingFactTypeResolver
{
    public static string Resolve(InvoiceDirection direction, InvoiceDocumentType documentType) => (direction, documentType) switch
    {
        (InvoiceDirection.Receivable, InvoiceDocumentType.Invoice) => AccountingFactTypes.CustomerInvoicePosted,
        (InvoiceDirection.Receivable, InvoiceDocumentType.CreditNote) => AccountingFactTypes.CustomerCreditNotePosted,
        (InvoiceDirection.Receivable, InvoiceDocumentType.DebitNote) => AccountingFactTypes.CustomerDebitNotePosted,
        (InvoiceDirection.Payable, InvoiceDocumentType.Invoice) => AccountingFactTypes.VendorBillPosted,
        (InvoiceDirection.Payable, InvoiceDocumentType.CreditNote) => AccountingFactTypes.VendorCreditNotePosted,
        (InvoiceDirection.Payable, InvoiceDocumentType.DebitNote) => AccountingFactTypes.VendorDebitNotePosted,
        _ => throw new InvoiceException("invoicing.document_type_invalid", "The invoice direction and document type are not supported.")
    };
}
