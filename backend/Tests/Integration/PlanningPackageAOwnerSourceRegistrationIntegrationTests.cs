using Microsoft.Extensions.DependencyInjection;
using Modules.Banking;
using Modules.CRM;
using Modules.FinanceBase;
using Modules.Hr;
using Modules.Inventory;
using Modules.Manufacturing;
using Modules.Projects;
using Modules.Sales;
using Modules.Subledger;
using Shared.Contracts.Planning;
using Xunit;

namespace KernelIntegrationTests;

public sealed class PlanningPackageAOwnerSourceRegistrationIntegrationTests
{
    [Fact(DisplayName = "Package A owner modules register one Planning source each")]
    public void OwnerModulesRegisterPlanningSources()
    {
        var services = new ServiceCollection();
        services.AddSalesModule();
        services.AddCrmModule();
        services.AddHrModule();
        services.AddBankingModule();
        services.AddInventoryModule();
        services.AddManufacturingModule();
        services.AddProjectsModule();
        services.AddSubledgerModule();
        services.AddFinanceBaseModule();

        var expected = new[]
        {
            typeof(Modules.Sales.Repositories.SalesPlanningOwnerSource),
            typeof(Modules.CRM.Repositories.CrmPlanningOwnerSource),
            typeof(Modules.Hr.Repositories.HrPlanningOwnerSource),
            typeof(Modules.Banking.Repositories.BankingPlanningOwnerSource),
            typeof(Modules.Inventory.Repositories.InventoryPlanningOwnerSource),
            typeof(Modules.Manufacturing.Repositories.ManufacturingPlanningOwnerSource),
            typeof(Modules.Projects.Repositories.ProjectsPlanningOwnerSource),
            typeof(Modules.Subledger.Repositories.SubledgerPlanningOwnerSource),
            typeof(Modules.FinanceBase.Repositories.FinanceBasePlanningOwnerSource)
        };

        Assert.Equal(expected.Length, services.Count(descriptor => descriptor.ServiceType == typeof(IPlanningOwnerReconciliationSource)));
        foreach (var providerType in expected)
            Assert.Contains(services, descriptor => descriptor.ServiceType == providerType && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
