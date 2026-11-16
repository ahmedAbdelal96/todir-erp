using Modules.Service.Domain;
using Xunit;

namespace Modules.Service.Tests;

public sealed class ServiceRulesTests
{
    [Fact]
    public void CustomerFieldServiceRequiresCustomer()
    {
        Assert.Throws<ArgumentException>(() => ServiceRules.ValidateContext("CustomerFieldService", null, Guid.NewGuid()));
    }

    [Fact]
    public void CustomerFieldServiceRequiresInstalledBase()
    {
        Assert.Throws<ArgumentException>(() => ServiceRules.ValidateContext("CustomerFieldService", Guid.NewGuid(), null));
    }

    [Fact]
    public void PartMovementCannotConsumeMoreThanIssued()
    {
        Assert.Throws<InvalidOperationException>(() => ServiceRules.ApplyPartMovement("consume", 2m, 10m, 0m, 1m, 0m, 0m));
    }

    [Fact]
    public void PartMovementIsMonotonicAndBounded()
    {
        var result = ServiceRules.ApplyPartMovement("consume", 2m, 10m, 5m, 5m, 1m, 1m);
        Assert.Equal(3m, result.Consumed);
        Assert.Equal(1m, result.Returned);
        Assert.Throws<InvalidOperationException>(() => ServiceRules.ApplyPartMovement("issue", 6m, 10m, 0m, 5m, 0m, 0m));
    }
}
