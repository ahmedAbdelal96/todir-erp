using System.Security.Cryptography;

namespace DbMigrator.SeedData;

public static class SeedIds
{
    public const string Version = "2026-10-10";

    public static Guid AtlasTenant { get; } = Stable("tenant:atlas");
    public static Guid DeltaTenant { get; } = Stable("tenant:delta");

    public static Guid TenantEntity(Guid tenantId) => Stable($"{tenantId:D}:legal-entity");
    public static Guid TenantSecondaryEntity(Guid tenantId) => Stable($"{tenantId:D}:legal-entity:secondary");
    public static Guid TenantBranch(Guid tenantId) => Stable($"{tenantId:D}:branch:main");
    public static Guid TenantSecondaryBranch(Guid tenantId) => Stable($"{tenantId:D}:branch:secondary");
    public static Guid Role(Guid tenantId, string code) => Stable($"{tenantId:D}:role:{code}");
    public static Guid Identity(Guid tenantId, string prefix) => Stable($"{tenantId:D}:identity:{prefix}");
    public static Guid Membership(Guid tenantId, string prefix) => Stable($"{tenantId:D}:membership:{prefix}");
    public static Guid Party(Guid tenantId, int index) => Stable($"{tenantId:D}:party:{index:D4}");
    public static Guid Category(Guid tenantId, int index) => Stable($"{tenantId:D}:category:{index:D2}");
    public static Guid Brand(Guid tenantId, int index) => Stable($"{tenantId:D}:brand:{index:D2}");
    public static Guid Uom(Guid tenantId, string code) => Stable($"{tenantId:D}:uom:{code}");
    public static Guid Product(Guid tenantId, int index) => Stable($"{tenantId:D}:product:{index:D4}");
    public static Guid Variant(Guid tenantId, int productIndex, string suffix) => Stable($"{tenantId:D}:variant:{productIndex:D4}:{suffix}");
    public static Guid Child(Guid tenantId, string type, int index) => Stable($"{tenantId:D}:{type}:{index:D5}");

    public static Guid Stable(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"development-seed:{Version}:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }
}
