namespace Modules.Sales.Entities;

public sealed class SalesException(string code, string? message = null) : Exception(message ?? code)
{
    public string Code { get; } = code;
}

public enum QuotationState { Draft, Open, Accepted, Declined, Expired, Cancelled, Converted }
public enum SalesOrderCommercialState { Draft, Confirmed, Cancelled }
public enum SalesOrderFulfillmentState { NotRequired, Pending, PartiallyFulfilled, Fulfilled, Backordered }
public enum SalesOrderInvoicingState { NotInvoiced, PartiallyInvoiced, Invoiced }
public enum SalesCreditState { NotChecked, Allowed, Warn, Blocked, ApprovalRequired }
public enum SalesReturnState { Requested, Approved, Executed, Rejected, Cancelled }
