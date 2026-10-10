using Core.Identity;
using Xunit;

namespace Core.Tests;

public sealed class IdentityPolicyTests
{
    [Fact]
    public void LoginDomainAndPrefixAreCanonicalAndRejectUnsafeInput()
    {
        Assert.Equal("atlas.com", LoginDomainConventions.NormalizeDomain(" Atlas.COM "));
        Assert.Equal("ahmed.sale", LoginDomainConventions.NormalizePrefix(" Ahmed.Sale "));
        Assert.Throws<ArgumentException>(() => LoginDomainConventions.NormalizeDomain("https://atlas.com/path"));
        Assert.Throws<ArgumentException>(() => LoginDomainConventions.NormalizePrefix("ahmed@example.com"));
        Assert.Equal("ahmed@atlas.com", LoginDomainConventions.BuildEmail("Ahmed", "Atlas.COM"));
    }

    [Fact]
    public void InitialPasswordPolicyRequiresLengthAndCharacterClasses()
    {
        Assert.False(PasswordPolicy.IsValid("Short1A"));
        Assert.False(PasswordPolicy.IsValid("longpasswordonly"));
        Assert.True(PasswordPolicy.IsValid("LongPassword123"));
    }
}
