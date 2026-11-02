namespace Modules.Hr.Entities;

public interface IHrCountryProvider
{
    string Code { get; }
    string Version { get; }
    bool ValidateEmployeeIdentifier(string identifierType, string value);
    IReadOnlyList<HrLeaveTypeTemplate> GetLeaveTypeTemplates();
    IReadOnlyList<HrHolidayDefinition> GetHolidays(int year);
}

public sealed record HrLeaveTypeTemplate(string Code, string NameAr, string? NameEn, string Category, string Unit, string RuleReference);
public sealed record HrHolidayDefinition(DateOnly Date, string NameAr, string? NameEn, string SourceReference);

public interface IHrCountryProviderRegistry
{
    IHrCountryProvider? Find(string code);
}

public sealed class HrCountryProviderRegistry(IEnumerable<IHrCountryProvider> providers) : IHrCountryProviderRegistry
{
    private readonly Dictionary<string, IHrCountryProvider> _providers = Build(providers);

    public IHrCountryProvider? Find(string code) => _providers.TryGetValue(code.Trim(), out var provider) ? provider : null;

    private static Dictionary<string, IHrCountryProvider> Build(IEnumerable<IHrCountryProvider> providers)
    {
        var registered = new Dictionary<string, IHrCountryProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Code) || string.IsNullOrWhiteSpace(provider.Version)
                || !registered.TryAdd(provider.Code.Trim(), provider))
                throw new InvalidOperationException("HR country provider codes must be unique and versioned.");
        }
        return registered;
    }
}
