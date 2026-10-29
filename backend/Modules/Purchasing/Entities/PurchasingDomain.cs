namespace Modules.Purchasing.Entities;

public static class PurchaseOrderStates
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Sent = "Sent";
    public const string Open = "Open";
    public const string PartiallyCompleted = "PartiallyCompleted";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Closed = "Closed";
}

public sealed class PurchasingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public PurchasingException(string code) : this(code, code) { }
}
