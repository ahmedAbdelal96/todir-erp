using Shared.Primitives.Identifiers;

namespace Modules.Payroll.Services;

public sealed record StatutoryPayrollInput(decimal GrossEarnings, string CurrencyCode, DateOnly PeriodEnd, Guid EmploymentId, IReadOnlyDictionary<string, decimal> Context);
public sealed record StatutoryPayrollOutput(IReadOnlyList<PayrollCalculationLine> EmployeeDeductions, IReadOnlyList<PayrollCalculationLine> EmployerContributions, string ProviderCode, string ProviderVersion, bool LegallyCertified);

public interface IPayrollCountryProvider
{
    string Code { get; }
    string Version { get; }
    bool LegallyCertified { get; }
    StatutoryPayrollOutput Calculate(StatutoryPayrollInput input);
    void ValidateConfiguration(DateOnly asOf);
}

public interface IPayrollCountryProviderRegistry
{
    IPayrollCountryProvider? Find(string code);
}

/// <summary>No statutory provider is enabled until approved legal rules and golden vectors are configured.</summary>
public sealed class PayrollCountryProviderRegistry : IPayrollCountryProviderRegistry
{
    public IPayrollCountryProvider? Find(string code) => null;
}

public sealed class PayrollCountryProviderRequiredException(string countryCode) : Exception($"payroll.country_configuration_missing:{countryCode}");
