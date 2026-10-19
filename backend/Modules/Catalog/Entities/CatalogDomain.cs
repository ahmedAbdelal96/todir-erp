using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Modules.Catalog.Entities;

public enum ProductNature { Goods, Service }
public enum CatalogStatus { Active, Archived }
public enum UomDimension { Count, Mass, Length, Area, Volume, Time, BusinessUnit }
public enum BarcodeType { Ean13, Ean8, Upc, Code128, Other }

public static partial class CatalogNormalization
{
    public static string Text(string value) => Collapse(value).Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    public static string Search(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormKD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark || character == '\u0640') continue;
            normalized.Append(character switch
            {
                '\u0622' or '\u0623' or '\u0625' or '\u0671' => '\u0627',
                '\u0649' => '\u064A',
                '\u0660' => '0', '\u0661' => '1', '\u0662' => '2', '\u0663' => '3', '\u0664' => '4',
                '\u0665' => '5', '\u0666' => '6', '\u0667' => '7', '\u0668' => '8', '\u0669' => '9',
                '\u06F0' => '0', '\u06F1' => '1', '\u06F2' => '2', '\u06F3' => '3', '\u06F4' => '4',
                '\u06F5' => '5', '\u06F6' => '6', '\u06F7' => '7', '\u06F8' => '8', '\u06F9' => '9',
                _ => character
            });
        }

        return Collapse(SearchSeparators().Replace(normalized.ToString().Normalize(NormalizationForm.FormKC).ToLowerInvariant(), " "));
    }
    public static string Barcode(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    private static string Collapse(string value) => Whitespace().Replace(value.Trim(), " ");
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();
    [GeneratedRegex("[^\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)] private static partial Regex SearchSeparators();
}

public sealed class ProductTemplate
{
    private ProductTemplate(Guid id, string name, ProductNature nature, Guid? categoryId, Guid? brandId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new CatalogDomainException("catalog.name_required", "Product name is required.");
        Id = id; DisplayName = name.Trim(); Nature = nature; CategoryId = categoryId; BrandId = brandId; Status = CatalogStatus.Active; Version = 1;
    }
    public Guid Id { get; }
    public string DisplayName { get; private set; }
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public ProductNature Nature { get; }
    public Guid? CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public CatalogStatus Status { get; private set; }
    public long Version { get; private set; }
    public static ProductTemplate Create(Guid id, string name, ProductNature nature, Guid? categoryId = null, Guid? brandId = null) => new(id, name, nature, categoryId, brandId);
    public void ChangeIdentity(string name, string? nameAr, string? nameEn, Guid? categoryId, Guid? brandId)
    {
        if (Status == CatalogStatus.Archived) throw new CatalogDomainException("catalog.archived_read_only", "Archived products cannot be edited.");
        if (string.IsNullOrWhiteSpace(name)) throw new CatalogDomainException("catalog.name_required", "Product name is required.");
        DisplayName = name.Trim(); NameAr = Null(nameAr); NameEn = Null(nameEn); CategoryId = categoryId; BrandId = brandId; Version++;
    }
    public void Archive() { Status = CatalogStatus.Archived; Version++; }
    public void Restore() { Status = CatalogStatus.Active; Version++; }
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProductVariant
{
    private ProductVariant(Guid id, Guid templateId, string? sku, bool isDefault)
    {
        Id = id; TemplateId = templateId; Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(); NormalizedSku = Sku is null ? null : CatalogNormalization.Text(Sku); IsDefault = isDefault; Status = CatalogStatus.Active; Version = 1;
    }
    public Guid Id { get; }
    public Guid TemplateId { get; }
    public string? Sku { get; private set; }
    public string? NormalizedSku { get; private set; }
    public bool IsDefault { get; }
    public CatalogStatus Status { get; private set; }
    public long Version { get; private set; }
    public static ProductVariant Create(Guid id, Guid templateId, string? sku = null, bool isDefault = true) => new(id, templateId, sku, isDefault);
    public void ChangeSku(string? sku) { if (Status == CatalogStatus.Archived) throw new CatalogDomainException("catalog.archived_read_only", "Archived variants cannot be edited."); Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(); NormalizedSku = Sku is null ? null : CatalogNormalization.Text(Sku); Version++; }
    public void Archive() { Status = CatalogStatus.Archived; Version++; }
    public void Restore() { Status = CatalogStatus.Active; Version++; }
}

public sealed class CategoryNode
{
    public CategoryNode(Guid id, string name, Guid? parentId = null) { if (string.IsNullOrWhiteSpace(name)) throw new CatalogDomainException("catalog.category_name_required", "Category name is required."); Id = id; Name = name.Trim(); ParentId = parentId; }
    public Guid Id { get; }
    public string Name { get; private set; }
    public Guid? ParentId { get; private set; }
    public CatalogStatus Status { get; private set; } = CatalogStatus.Active;
    public void Move(Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents) { if (parentId == Id) throw new CatalogDomainException("catalog.category_self_parent", "A category cannot parent itself."); var seen = new HashSet<Guid> { Id }; for (var current = parentId; current is not null; current = parents.GetValueOrDefault(current.Value)) if (!seen.Add(current.Value)) throw new CatalogDomainException("catalog.category_cycle", "Category hierarchy cannot contain a cycle."); ParentId = parentId; }
    public void Archive() => Status = CatalogStatus.Archived;
}

public static class QuantityConversion
{
    public static decimal ToBase(decimal quantity, decimal factor, bool allowFraction, int precision) { if (factor <= 0) throw new CatalogDomainException("catalog.conversion_factor_invalid", "Conversion factor must be positive."); if (!allowFraction && quantity != decimal.Truncate(quantity)) throw new CatalogDomainException("catalog.fraction_not_allowed", "This unit does not allow fractional quantities."); return decimal.Round(quantity * factor, precision, MidpointRounding.ToEven); }
}

public sealed class CatalogDomainException(string code, string message) : Exception(message) { public string Code { get; } = code; }
