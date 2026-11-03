using Core.Services;
using Xunit;

namespace Core.Tests;

public sealed class PlatformFileUploadPolicyTests
{
    [Fact]
    public void AcceptsSupportedOwnerScopedRequestAndMatchingPdfSignature()
    {
        var owner = Guid.CreateVersion7();
        var validated = PlatformFileUploadPolicy.ValidateRequest("HR", "Employee", owner, "contract.pdf", "application/pdf", 5, "confidential");

        Assert.Equal("hr", validated.OwnerModule);
        Assert.True(PlatformFileUploadPolicy.ContentMatches("contract.pdf", "application/pdf", "%PDF-"u8));
    }

    [Theory]
    [InlineData("../contract.pdf", "application/pdf", 5, "internal")]
    [InlineData("contract.exe", "application/pdf", 5, "internal")]
    [InlineData("contract.pdf", "application/pdf", 0, "internal")]
    [InlineData("contract.pdf", "application/pdf", 26214401, "internal")]
    [InlineData("contract.pdf", "application/pdf", 5, "public")]
    public void RejectsUnsafeOrInvalidUploadRequest(string name, string contentType, long size, string classification)
    {
        Assert.Throws<PlatformFileContentRejectedException>(() => PlatformFileUploadPolicy.ValidateRequest("hr", "employee", Guid.CreateVersion7(), name, contentType, size, classification));
    }

    [Fact]
    public void RejectsDeclaredPdfWithoutPdfMagic()
    {
        Assert.False(PlatformFileUploadPolicy.ContentMatches("contract.pdf", "application/pdf", "not a pdf"u8));
    }
}
