using Modules.Sales.DTOs;
using Modules.Sales.Entities;
using Xunit;

namespace SalesTests;

public sealed class SalesDomainTests
{
    [Fact]
    public void AcceptedRevisionAndConversionStatesAreExplicit()
    {
        Assert.Equal("Accepted", QuotationState.Accepted.ToString());
        Assert.Equal("Converted", QuotationState.Converted.ToString());
        Assert.Equal("Draft", SalesOrderCommercialState.Draft.ToString());
        Assert.Equal("PartiallyFulfilled", SalesOrderFulfillmentState.PartiallyFulfilled.ToString());
    }

    [Fact]
    public void CommercialLinePreservesQuantityAndMoneyAsDecimal()
    {
        var line = new QuotationLineRequest(Guid.NewGuid(), Guid.NewGuid(), "Service", 2.5m, 100.25m, 0m, "EA");
        Assert.Equal(250.625m, line.Quantity * line.UnitPrice);
        Assert.True(line.Quantity > 0m);
    }
}
