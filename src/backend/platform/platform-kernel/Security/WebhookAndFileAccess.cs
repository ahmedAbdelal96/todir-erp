using System.Security.Cryptography;
using System.Text;
using Todir.Platform.Services;
using Todir.SharedKernel.Identifiers;

namespace Todir.Platform.Security;

public sealed class HmacWebhookVerifier
{
    private readonly byte[] secret;
    private readonly TimeSpan tolerance;

    public HmacWebhookVerifier(string secret, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("webhook.secret_required", nameof(secret));
        this.secret = Encoding.UTF8.GetBytes(secret);
        this.tolerance = tolerance ?? TimeSpan.FromMinutes(5);
    }

    public bool Verify(ReadOnlySpan<byte> body, string timestamp, string signature, DateTimeOffset now)
    {
        if (!long.TryParse(timestamp, out var unixSeconds) || Math.Abs(now.ToUnixTimeSeconds() - unixSeconds) > tolerance.TotalSeconds) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); } catch (FormatException) { return false; }
        var payload = Encoding.UTF8.GetBytes(timestamp + "." + Encoding.UTF8.GetString(body));
        var expected = HMACSHA256.HashData(secret, payload);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

public interface IFileAccessSigner
{
    string CreateDownloadUrl(FileRecord file, TenantId tenantId, DateTimeOffset expiresAt);
    bool VerifyDownload(FileRecord file, TenantId tenantId, DateTimeOffset expiresAt, string signature, DateTimeOffset now);
}

public sealed class HmacFileAccessSigner(string secret) : IFileAccessSigner
{
    private readonly byte[] key = CreateKey(secret);

    public string CreateDownloadUrl(FileRecord file, TenantId tenantId, DateTimeOffset expiresAt)
    {
        if (file.TenantId != tenantId || file.Status != FileStatus.Ready) throw new InvalidOperationException("file.access_denied");
        if (expiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("file.expiry_required");
        return $"/api/v1/files/{file.Id:D}?expires={expiresAt.ToUnixTimeSeconds()}&signature={CreateSignature(file, tenantId, expiresAt)}";
    }

    public bool VerifyDownload(FileRecord file, TenantId tenantId, DateTimeOffset expiresAt, string signature, DateTimeOffset now)
    {
        if (file.TenantId != tenantId || file.Status != FileStatus.Ready || expiresAt <= now) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(CreateSignature(file, tenantId, expiresAt)), Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }

    private string CreateSignature(FileRecord file, TenantId tenantId, DateTimeOffset expiresAt)
    {
        var payload = $"{tenantId.Value:D}\n{file.Id:D}\n{file.ObjectKey}\n{expiresAt.ToUnixTimeSeconds()}";
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static byte[] CreateKey(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("file.signing_secret_required", nameof(secret));
        return Encoding.UTF8.GetBytes(secret);
    }
}
