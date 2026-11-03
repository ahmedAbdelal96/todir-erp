using Core.Identity;
using Xunit;

namespace Core.Tests;

public sealed class UserNavigationPreferencesTests
{
    [Theory]
    [InlineData("home")]
    [InlineData("domain-settings")]
    [InlineData("subledger")]
    public void NavigationKeyAcceptsStableRouteIdentifiers(string value) => Assert.True(UserNavigationKey.IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("../admin")]
    [InlineData("Home")]
    [InlineData("users/")]
    [InlineData("users--admin")]
    [InlineData("users-")]
    public void NavigationKeyRejectsPathsAndUnstableIdentifiers(string value) => Assert.False(UserNavigationKey.IsValid(value));
}
