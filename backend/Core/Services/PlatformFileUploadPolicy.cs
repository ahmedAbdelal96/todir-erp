using System.Text;

namespace Core.Services;

public static class PlatformFileUploadPolicy
{
    public const long MaximumSizeBytes = 25 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public sealed record ValidatedUpload(string OwnerModule, string OwnerEntityType, string OriginalName, string ContentType, string Classification);

    public static ValidatedUpload ValidateRequest(string ownerModule, string ownerEntityType, Guid ownerEntityId, string originalName, string contentType, long sizeBytes, string classification)
    {
        var module = (ownerModule ?? string.Empty).Trim().ToLowerInvariant();
        var entityType = (ownerEntityType ?? string.Empty).Trim().ToLowerInvariant();
        var name = (originalName ?? string.Empty).Trim();
        var mediaType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var sensitivity = (classification ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsSafeToken(module, 40) || !IsSafeToken(entityType, 60) || ownerEntityId == Guid.Empty
            || name.Length is < 1 or > 255 || name.IndexOfAny(['/', '\\']) >= 0 || name.Any(char.IsControl)
            || sizeBytes is < 1 or > MaximumSizeBytes || !ValidNameTypePair(name, mediaType)
            || sensitivity is not ("internal" or "confidential" or "medical" or "restricted"))
            throw new PlatformFileContentRejectedException("files.upload_request_invalid");
        return new(module, entityType, name, mediaType, sensitivity);
    }

    public static bool ContentMatches(string originalName, string contentType, ReadOnlySpan<byte> content)
    {
        if (!ValidNameTypePair(originalName, contentType) || content.IsEmpty) return false;
        return contentType switch
        {
            "application/pdf" => content.StartsWith("%PDF-"u8),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "text/plain" => IsPlainUtf8(content),
            _ => false
        };
    }

    private static bool IsSafeToken(string value, int maximumLength) => value.Length is > 0 && value.Length <= maximumLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static bool ValidNameTypePair(string name, string contentType)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        return contentType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "text/plain" => extension == ".txt",
            _ => false
        };
    }

    private static bool IsPlainUtf8(ReadOnlySpan<byte> content)
    {
        if (content.Contains((byte)0)) return false;
        try { _ = StrictUtf8.GetCharCount(content); return true; }
        catch (DecoderFallbackException) { return false; }
    }
}
