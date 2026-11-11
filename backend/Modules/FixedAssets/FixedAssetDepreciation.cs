namespace Modules.FixedAssets;

public static class DepreciationEngine
{
    public static decimal CalculateStraightLine(decimal depreciableBasis, decimal residualValue, int usefulLifeMonths, int monthsElapsed, int scale = 2)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depreciableBasis);
        if (residualValue < 0m || residualValue > depreciableBasis) throw new ArgumentOutOfRangeException(nameof(residualValue));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usefulLifeMonths);
        if (monthsElapsed <= 0) return 0m;
        var monthly = decimal.Round((depreciableBasis - residualValue) / usefulLifeMonths, scale, MidpointRounding.ToEven);
        var accumulated = decimal.Round(monthly * Math.Min(monthsElapsed, usefulLifeMonths), scale, MidpointRounding.ToEven);
        return Math.Min(accumulated, decimal.Round(depreciableBasis - residualValue, scale, MidpointRounding.ToEven));
    }

    public static decimal PeriodAmount(decimal depreciableBasis, decimal residualValue, int usefulLifeMonths, int monthsElapsed, int scale = 2)
    {
        var before = CalculateStraightLine(depreciableBasis, residualValue, usefulLifeMonths, monthsElapsed - 1, scale);
        var after = CalculateStraightLine(depreciableBasis, residualValue, usefulLifeMonths, monthsElapsed, scale);
        return after - before;
    }
}
