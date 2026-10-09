using Todir.Modules.Directory.Domain;
using Xunit;

namespace DirectoryTests;

public sealed class DirectoryNormalizationTests
{
    [Fact]
    public void ArabicNamesAndDigitsNormalizeDeterministically()
    {
        Assert.Equal("احمد علي 123", DirectoryNormalization.Name(" أَحْمَد  عَلَى ١٢٣ "));
        Assert.Equal("123", DirectoryNormalization.Identifier(" ١٢٣ "));
    }

    [Fact]
    public void EmailAndPhoneNormalizationIsStable()
    {
        Assert.Equal("person@example.com", DirectoryNormalization.Email(" Person@Example.COM "));
        Assert.Equal("+201012345678", DirectoryNormalization.Phone("+٢٠١٠ ١٢٣٤٥٦٧٨"));
    }
}
