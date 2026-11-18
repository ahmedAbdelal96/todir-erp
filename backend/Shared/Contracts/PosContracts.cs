namespace Shared.Contracts.POS;

public enum PosFiscalSubmissionStrategy
{
    Synchronous,
    Deferred
}

public sealed record PosFiscalSubmissionRequest(Guid TenantId, Guid LegalEntityId, Guid SaleId, Guid InvoiceId,
    string CurrencyCode, decimal Total, string ReceiptNumber);

public sealed record PosFiscalSubmissionResult(string State, string? FiscalReference, bool Deferred);

/// <summary>
/// Country/compliance providers own fiscal payloads, signing, submission, and statutory responses.
/// POS core only consumes the state result and never branches on a country or tax jurisdiction.
/// </summary>
public interface IPosFiscalProvider
{
    string ProviderCode { get; }
    PosFiscalSubmissionStrategy Strategy { get; }

    Task<PosFiscalSubmissionResult> SubmitAsync(PosFiscalSubmissionRequest request,
        CancellationToken cancellationToken = default);
}

public enum PosHardwareCommandKind
{
    PrintReceipt,
    ScanBarcode,
    OpenCashDrawer,
    ShowCustomerMessage,
    CaptureCard
}

public sealed record PosHardwareCommand(PosHardwareCommandKind Kind, string Payload);
public sealed record PosHardwareResult(string State, string? DeviceReference, string? Detail);

/// <summary>Immutable POS close evidence handed to Banking; POS remains the owner of the session.</summary>
public sealed record PosSessionClosedV1(Guid LegalEntityId, Guid RegisterId, Guid? BranchId, Guid SessionId,
    DateOnly BusinessDate, string CurrencyCode, decimal ExpectedCash, decimal CountedCash, decimal Variance);

/// <summary>
/// Hardware adapters return device observations only; they cannot create or mutate POS domain truth.
/// </summary>
public interface IPosHardwareAdapter
{
    string DeviceType { get; }

    Task<PosHardwareResult> ExecuteAsync(PosHardwareCommand command,
        CancellationToken cancellationToken = default);
}
