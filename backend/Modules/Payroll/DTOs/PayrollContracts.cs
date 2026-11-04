namespace Modules.Payroll.DTOs;

public sealed record CreatePayrollGroupRequest(Guid LegalEntityId, string Code, string Name, string Frequency, short PayDay, string CurrencyCode, string? HolidayAdjustment = null);
public sealed record CreatePayrollPeriodRequest(Guid PayrollGroupId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly InputCutoff, DateOnly CalculationDate, DateOnly PlannedPayDate);
public sealed record CreatePayrollRunRequest(Guid PayrollGroupId, Guid? PeriodId, string RunType = "Regular");
public sealed record CreatePayrollInputRequest(Guid LegalEntityId, Guid EmploymentId, Guid ComponentId, DateOnly InputDate, decimal Amount, string CurrencyCode, string Source, string? IdempotencyKey = null, string? Notes = null);
public sealed record CreateReceivableRequest(Guid LegalEntityId, Guid EmploymentId, string Type, decimal Principal, string CurrencyCode, decimal? InstallmentAmount = null, int? InstallmentCount = null);
public sealed record PayrollGroupDto(Guid Id, Guid LegalEntityId, string Code, string Name, string Frequency, short PayDay, string CurrencyCode, string Status);
public sealed record PayrollPeriodDto(Guid Id, Guid PayrollGroupId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly InputCutoff, DateOnly PlannedPayDate, string Status);
public sealed record PayrollRunDto(Guid Id, Guid PayrollGroupId, Guid? PeriodId, string RunNumber, string RunType, string CalculationState, string FinalizationState, string PaymentState, decimal TotalGross, decimal TotalDeductions, decimal TotalNet, int EmployeeCount);
public sealed record PayrollEmployeeDto(Guid EmploymentId, string Status, decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, string? ExceptionCode);
public sealed record PayslipDto(Guid Id, string PayslipNumber, Guid EmploymentId, decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, string Status, DateTimeOffset? FinalizedAt);
public sealed record EmployeeReceivableDto(Guid Id, Guid EmploymentId, string Type, decimal Principal, decimal OutstandingPrincipal, string CurrencyCode, string Status);
public sealed record ReceivableDisbursementDto(Guid Id, Guid ReceivableId, decimal Amount, string CurrencyCode, string Status, string? ProviderReference);
public sealed record PayrollStatutorySettingsDto(Guid Id, Guid LegalEntityId, string TaxMode, string InsuranceMode, DateOnly EffectiveFrom, int Version, DateTimeOffset CreatedAt, string ManualInputPolicy = "Block");
public sealed record UpdatePayrollStatutorySettingsRequest(string TaxMode, string InsuranceMode, DateOnly EffectiveFrom, int ExpectedVersion, string ManualInputPolicy = "Block");
public sealed record PayrollInputDto(Guid Id, Guid EmploymentId, Guid ComponentId, DateOnly InputDate, decimal Amount, string CurrencyCode, string Source, string Status, string? Notes);
public sealed record PayrollSnapshotDto(Guid SnapshotId, Guid RunId, DateOnly PeriodStart, DateOnly PeriodEnd, DateTimeOffset CutoffAt, string PayloadSha256, int EmploymentCount);
