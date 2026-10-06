using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Todir.Platform.Identity;

public sealed record MfaEnrollment(string SecretBase32, IReadOnlyList<string> RecoveryCodeDigests);
public interface IMfaService
{
    MfaEnrollment CreateEnrollment(int recoveryCodeCount = 10);
    bool VerifyTotp(string secretBase32, string code, DateTimeOffset now);
    bool VerifyRecoveryCode(string code, IReadOnlyCollection<string> recoveryCodeDigests);
}

public sealed class MfaService : IMfaService
{
    public MfaEnrollment CreateEnrollment(int recoveryCodeCount = 10)
    {
        if (recoveryCodeCount is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(recoveryCodeCount));
        var secret = RandomNumberGenerator.GetBytes(20);
        var codes = Enumerable.Range(0, recoveryCodeCount).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
        return new MfaEnrollment(Base32Encode(secret), codes.Select(Hash).ToArray());
    }

    public bool VerifyTotp(string secretBase32, string code, DateTimeOffset now)
    {
        if (code.Length != 6 || !code.All(char.IsDigit)) return false;
        byte[] secret;
        try { secret = Base32Decode(secretBase32); } catch (FormatException) { return false; }
        var counter = Math.Max(0, now.ToUnixTimeSeconds() / 30);
        for (var offset = -1; offset <= 1; offset++) if (GenerateCode(secret, (ulong)Math.Max(0, counter + offset)) == code) return true;
        return false;
    }

    public bool VerifyRecoveryCode(string code, IReadOnlyCollection<string> recoveryCodeDigests)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        foreach (var encoded in recoveryCodeDigests)
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(Convert.FromHexString(encoded), digest)) return true;
            }
            catch (FormatException) { }
        }

        return false;
    }

    // RFC 6238 TOTP interoperability requires HMAC-SHA1 for the initial MFA contract.
#pragma warning disable CA5350
    private static string GenerateCode(byte[] secret, ulong counter)
    {
        Span<byte> bytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(bytes, counter);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(bytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var value = (hash[offset] & 0x7f) << 24 | hash[offset + 1] << 16 | hash[offset + 2] << 8 | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
#pragma warning restore CA5350

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Base32Encode(byte[] data) { const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var output = new StringBuilder((data.Length * 8 + 4) / 5); var buffer = 0; var bits = 0; foreach (var b in data) { buffer = (buffer << 8) | b; bits += 8; while (bits >= 5) { output.Append(alphabet[(buffer >> (bits - 5)) & 31]); bits -= 5; } } if (bits > 0) output.Append(alphabet[(buffer << (5 - bits)) & 31]); return output.ToString(); }
    private static byte[] Base32Decode(string value) { const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var buffer = 0; var bits = 0; var output = new List<byte>(); foreach (var character in value.TrimEnd('=').ToUpperInvariant()) { var index = alphabet.IndexOf(character); if (index < 0) throw new FormatException(); buffer = (buffer << 5) | index; bits += 5; if (bits >= 8) { output.Add((byte)(buffer >> (bits - 8))); bits -= 8; } } return output.ToArray(); }
}
