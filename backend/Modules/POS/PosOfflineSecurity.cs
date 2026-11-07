using System.Security.Cryptography;
using System.Text;

namespace Modules.POS;

public static class PosOfflineSecurity
{
    public static string SignEnvelope(
        string packageKey,
        Guid registerId,
        string deviceIdentity,
        long sourceSequence,
        string idempotencyKey,
        long packageVersion,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageKey);
        var canonical = CanonicalEnvelope(registerId, deviceIdentity, sourceSequence, idempotencyKey, packageVersion, payload);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(packageKey), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool VerifyEnvelope(
        string packageKey,
        Guid registerId,
        string deviceIdentity,
        long sourceSequence,
        string idempotencyKey,
        long packageVersion,
        string payload,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(packageKey) || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = SignEnvelope(packageKey, registerId, deviceIdentity, sourceSequence, idempotencyKey, packageVersion, payload);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim()));
    }

    private static string CanonicalEnvelope(Guid registerId, string deviceIdentity, long sourceSequence,
        string idempotencyKey, long packageVersion, string payload)
        => string.Join('|', registerId.ToString("D"), deviceIdentity.Trim(), sourceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            idempotencyKey.Trim(), packageVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), payload);
}
