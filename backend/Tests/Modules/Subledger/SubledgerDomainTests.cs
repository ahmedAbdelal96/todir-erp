using Modules.Subledger.Entities;
using Modules.Subledger.Services;
using Core.Authorization;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;
using Xunit;

namespace Subledger.Tests;

public sealed class SubledgerDomainTests
{
    [Theory]
    [InlineData(SubledgerDirection.Receivable, OpenItemPolarity.Debit)]
    [InlineData(SubledgerDirection.Payable, OpenItemPolarity.Credit)]
    public void SourceItemsHaveDirectionSpecificPolarity(SubledgerDirection direction, OpenItemPolarity expected) =>
        Assert.Equal(expected, SubledgerRules.SourcePolarity(direction));

    [Theory]
    [InlineData(SubledgerDirection.Receivable, OpenItemPolarity.Credit)]
    [InlineData(SubledgerDirection.Payable, OpenItemPolarity.Debit)]
    public void PaymentCreditsHaveOppositePolarity(SubledgerDirection direction, OpenItemPolarity expected) =>
        Assert.Equal(expected, SubledgerRules.PaymentPolarity(direction));

    [Theory]
    [InlineData(0, 100, OpenItemStatus.Open)]
    [InlineData(25, 75, OpenItemStatus.PartiallySettled)]
    [InlineData(100, 0, OpenItemStatus.Settled)]
    public void StatusIsDerivedFromSettledAndOpenAmounts(decimal settled, decimal open, OpenItemStatus expected) =>
        Assert.Equal(expected, SubledgerRules.StatusFor(settled, open));

    [Fact]
    public void ApplicationAmountMustBePositive()
    {
        Assert.Throws<SubledgerException>(() => SubledgerRules.ValidatePositiveAmount(0m));
        Assert.Throws<SubledgerException>(() => SubledgerRules.ValidatePositiveAmount(-0.01m));
        SubledgerRules.ValidatePositiveAmount(0.01m);
    }

    [Fact]
    public void SourceCreationContractDoesNotAcceptCallerChosenProducerIdentity()
    {
        var propertyNames = typeof(CreateOpenItemsCommand).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("SourceType", propertyNames);
        Assert.DoesNotContain("SourceModule", propertyNames);
    }

    [Fact]
    public async Task WriteOffPolicyRequiresTheDedicatedPermission()
    {
        var service = new SubledgerService(null!, null!, null!, null!, new NullAuthorizationSubjectStore(), new PermissionAuthorizer(), TimeProvider.System);
        var exception = await Assert.ThrowsAsync<SubledgerException>(() => service.UpdateWriteOffPolicyAsync(
            new TenantId(Guid.CreateVersion7()), Guid.CreateVersion7(),
            new WriteOffPolicyCommand(new LegalEntityId(Guid.CreateVersion7()), SubledgerDirectionContract.Receivable, "EGP", "100", "10", 0)));
        Assert.Equal("subledger.permission_denied", exception.Code);
    }
}
