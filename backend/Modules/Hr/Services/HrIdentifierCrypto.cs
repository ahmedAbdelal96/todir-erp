namespace Modules.Hr.Services;

public sealed class HrIdentifierCrypto
{
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtector protector;
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtector lookupKeyProtector;
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtector insuranceProtector;

    public HrIdentifierCrypto(Microsoft.AspNetCore.DataProtection.IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("TODIR.HR.EmployeeIdentifier.v1");
        lookupKeyProtector = provider.CreateProtector("TODIR.HR.EmployeeIdentifierLookupKey.v1");
        insuranceProtector = provider.CreateProtector("TODIR.HR.InsuranceMemberNumber.v1");
    }

    public string Protect(string value) => Convert.ToBase64String(protector.Protect(System.Text.Encoding.UTF8.GetBytes(value)));
    public string Unprotect(string value) => System.Text.Encoding.UTF8.GetString(protector.Unprotect(Convert.FromBase64String(value)));
    public static byte[] CreateLookupKey() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    public string ProtectLookupKey(byte[] value) => Convert.ToBase64String(lookupKeyProtector.Protect(value));
    public byte[] UnprotectLookupKey(string value) => lookupKeyProtector.Unprotect(Convert.FromBase64String(value));
    public string ProtectInsuranceNumber(string value) => Convert.ToBase64String(insuranceProtector.Protect(System.Text.Encoding.UTF8.GetBytes(value)));
    public string UnprotectInsuranceNumber(string value) => System.Text.Encoding.UTF8.GetString(insuranceProtector.Unprotect(Convert.FromBase64String(value)));
    public static byte[] Hash(byte[] key, string normalizedValue) => System.Security.Cryptography.HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(normalizedValue));
    public static string Normalize(string value) => string.Concat((value ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormKC).Where(static character => !char.IsWhiteSpace(character))).ToUpperInvariant();
    public static string Mask(string value) => new string('•', Math.Max(4, value.Length - 4)) + (value.Length > 4 ? value[^4..] : string.Empty);
}
