namespace Modules.Pricing.Entities;

public sealed class PricingConfigurationConflictException(string message, IReadOnlyList<Guid> ruleIds) : PricingException("pricing.configuration_conflict", message)
{
    public IReadOnlyList<Guid> RuleIds { get; } = ruleIds;
}
