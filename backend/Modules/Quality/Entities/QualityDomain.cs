namespace Modules.Quality.Entities;

public sealed class QualityException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class QualityRules
{
    public static int CalculateSampleSize(string method, decimal lotQuantity, decimal? fixedQuantity, decimal? percentage)
    {
        if (lotQuantity <= 0m) throw new QualityException("quality.lot_quantity_invalid");
        if (method == "Full") return (int)Math.Ceiling(lotQuantity);
        if (method == "Fixed" && fixedQuantity is > 0m) return (int)Math.Min(Math.Ceiling(fixedQuantity.Value), Math.Ceiling(lotQuantity));
        if (method == "Percentage" && percentage is > 0m && percentage <= 100m) return (int)Math.Max(1m, Math.Ceiling(lotQuantity * percentage.Value / 100m));
        throw new QualityException("quality.sampling_invalid");
    }

    public static bool EvaluateNumeric(decimal? value, decimal? minimum, decimal? maximum, bool required)
        => value is not null && (!required || (minimum is null || value >= minimum) && (maximum is null || value <= maximum));

    public static bool EvaluateCategorical(string? value, IReadOnlySet<string> allowed, bool required)
        => !string.IsNullOrWhiteSpace(value) && (!required || allowed.Contains(value.Trim()));

    public static string NormalizeFingerprint(string value) => string.IsNullOrWhiteSpace(value) ? throw new QualityException("quality.fingerprint_required") : value.Trim();
}
