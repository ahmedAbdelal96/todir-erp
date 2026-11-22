using Core.Hosting;
using Core.Scheduling;
using Modules.Sample;
using MassTransit;
using Worker;
using Modules.Catalog;
using Modules.Directory;
using Modules.FinanceBase;
using Modules.Pricing;
using Modules.Payments;
using Modules.Banking;
using Modules.Ledger;
using Modules.Subledger;
using Modules.Inventory;
using Modules.Sales;
using Modules.Invoicing.Controllers;
using Modules.Tax;
using Modules.Hr;
using Modules.Payroll;
using Modules.CRM;
using Modules.Expenses;
using Modules.POS;
using Modules.Projects;
using Modules.Manufacturing;
using Modules.FixedAssets;
using Modules.Service;
using Modules.FixedAssets.Controllers;
using Quartz;
using Shared.BuildingBlocks.Observability;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddRepositoryConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);
builder.Services.AddObservability(builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
var connectionString = builder.Configuration.GetRuntimeConnectionString();
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseReadiness.WaitForPlatformSchemaAsync(connectionString, CancellationToken.None);

builder.Services.AddRouting();
builder.Services.AddCore(builder.Configuration, bus =>
{
    SampleModuleExtensions.ConfigureSampleMessaging(bus);
    PosModuleExtensions.ConfigurePosMessaging(bus);
    bus.AddConsumer<CatalogProjectionConsumer, CatalogProjectionConsumerDefinition>();
    bus.AddConsumer<CatalogImportConsumer, CatalogImportConsumerDefinition>();
    bus.AddConsumer<PaymentProviderOperationConsumer, PaymentProviderOperationConsumerDefinition>();
    bus.AddConsumer<BankingExternalFeedConsumer, BankingExternalFeedConsumerDefinition>();
    bus.AddConsumer<BankingPosSettlementConsumer, BankingPosSettlementConsumerDefinition>();
    bus.AddConsumer<BankingProviderSettlementConsumer, BankingProviderSettlementConsumerDefinition>();
    bus.AddConsumer<BankingOriginatedEventConsumer, BankingOriginatedEventConsumerDefinition>();
    bus.AddConsumer<HrMembershipDeactivationConsumer, HrMembershipDeactivationConsumerDefinition>();
    bus.AddConsumer<PayrollLedgerPostingConsumer, PayrollLedgerPostingConsumerDefinition>();
    bus.AddConsumer<PayrollPaymentRequestedConsumer, PayrollPaymentRequestedConsumerDefinition>();
    bus.AddConsumer<PayrollPaymentReconciliationConsumer, PayrollPaymentReconciliationConsumerDefinition>();
    bus.AddConsumer<CrmExternalEventConsumer, CrmExternalEventConsumerDefinition>();
    bus.AddConsumer<ExpensesOcrConsumer, ExpensesOcrConsumerDefinition>();
    bus.AddConsumer<ExpensesNotificationConsumer, ExpensesNotificationConsumerDefinition>();
    bus.AddConsumer<ProjectsNotificationConsumer, ProjectsNotificationConsumerDefinition>();
    bus.AddConsumer<ProjectsSourceEventConsumer, ProjectsSourceEventConsumerDefinition>();
    bus.AddConsumer<ProjectCapitalHandoffConsumer, ProjectCapitalHandoffConsumerDefinition>();
    bus.AddConsumer<ManufacturingIntegrationConsumer, ManufacturingIntegrationConsumerDefinition>();
    bus.AddConsumer<CacheInvalidationConsumer, CacheInvalidationConsumerDefinition>();
});
builder.Services.AddSampleModule();
builder.Services.AddCatalogModule();
builder.Services.AddDirectoryModule();
builder.Services.AddFinanceBaseModule();
builder.Services.AddPricingModule();
builder.Services.AddLedgerModule();
builder.Services.AddSubledgerModule();
builder.Services.AddPaymentsModule();
builder.Services.AddBankingModule();
builder.Services.AddInventoryModule();
builder.Services.AddInvoicingModule();
builder.Services.AddSalesModule();
builder.Services.AddTaxModule();
builder.Services.AddHrModule();
builder.Services.AddPayrollModule();
builder.Services.AddCrmModule();
builder.Services.AddExpensesModule();
builder.Services.AddPosModule();
builder.Services.AddProjectsModule();
builder.Services.AddManufacturingModule();
builder.Services.AddFixedAssetsModule();
builder.Services.AddServiceModule();
builder.Services.AddScheduling(builder.Configuration, options =>
{
    options.AddJob<HrExpiryReminderJob>(job => job
        .WithIdentity(HrExpiryReminderJob.Key)
        .StoreDurably()
        .RequestRecovery());
    options.AddTrigger(trigger => trigger
        .ForJob(HrExpiryReminderJob.Key)
        .WithIdentity(HrExpiryReminderJob.Trigger)
        .WithCronSchedule("0 0 2 * * ?", schedule => schedule
            .InTimeZone(TimeZoneInfo.Utc)
            .WithMisfireHandlingInstructionFireAndProceed()));
    options.AddJob<ExpensesReminderJob>(job => job
        .WithIdentity(ExpensesReminderJob.Key)
        .StoreDurably()
        .RequestRecovery());
    options.AddTrigger(trigger => trigger
        .ForJob(ExpensesReminderJob.Key)
        .WithIdentity(ExpensesReminderJob.Trigger)
        .WithCronSchedule("0 */15 * * * ?", schedule => schedule
            .InTimeZone(TimeZoneInfo.Utc)
            .WithMisfireHandlingInstructionFireAndProceed()));
});
builder.Services.AddHostedService<OutboxDispatchWorker>();
builder.Services.AddHostedService<FinanceBaseRateSyncWorker>();
builder.Services.AddHostedService<PricingScheduledActivationWorker>();
builder.Services.AddHostedService<InventoryReservationExpiryWorker>();
builder.Services.AddHostedService<ServiceMaintenanceWorker>();
await builder.Build().RunAsync();
