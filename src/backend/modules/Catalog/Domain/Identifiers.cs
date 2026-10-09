namespace Todir.Modules.Catalog.Domain;

public static class IdentifierRules
{
    public static string RequireBarcode(string value)
    {
        var normalized = CatalogNormalization.Barcode(value);
        if (normalized.Length is < 4 or > 64) throw new CatalogDomainException("catalog.barcode_invalid", "Barcode must contain between 4 and 64 alphanumeric characters.");
        return normalized;
    }
    public static string RequireCode(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new CatalogDomainException($"catalog.{field}_required", $"{field} is required.");
        return CatalogNormalization.Text(value);
    }
}
