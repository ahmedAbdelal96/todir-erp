namespace Modules.Banking.Rules;

public static class BankingRules
{
    public static void ValidateMatch(decimal externalAmount, decimal systemAmount, decimal difference)
    {
        if (externalAmount <= 0m || systemAmount < 0m || difference != externalAmount - systemAmount)
            throw new InvalidOperationException("A reconciliation match must conserve the external amount.");
    }

    public static decimal CalculateCashCount(IEnumerable<(decimal UnitValue, int Quantity)> denominations)
    {
        var values = denominations.ToArray();
        if (values.Length == 0 || values.Any(value => value.UnitValue <= 0m || value.Quantity < 0))
            throw new InvalidOperationException("Cash denominations must be positive and quantities cannot be negative.");
        return values.Sum(value => value.UnitValue * value.Quantity);
    }
}
