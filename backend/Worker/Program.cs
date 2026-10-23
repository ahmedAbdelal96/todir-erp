using Core.Hosting;
using Core.Scheduling;
using Modules.Sample;
using MassTransit;
using Worker;
using Modules.Catalog;
using Modules.Directory;
using Modules.FinanceBase;
using Modules.Pricing;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddRepositoryConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);
var connectionString = builder.Configuration.GetRuntimeConnectionString();
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseReadiness.WaitForPlatformSchemaAsync(connectionString, CancellationToken.None);

builder.Services.AddRouting();
builder.Services.AddCore(builder.Configuration, bus =>
{
    SampleModuleExtensions.ConfigureSampleMessaging(bus);
    bus.AddConsumer<CatalogProjectionConsumer, CatalogProjectionConsumerDefinition>();
    bus.AddConsumer<CatalogImportConsumer, CatalogImportConsumerDefinition>();
});
builder.Services.AddSampleModule();
builder.Services.AddCatalogModule();
builder.Services.AddDirectoryModule();
builder.Services.AddFinanceBaseModule();
builder.Services.AddPricingModule();
builder.Services.AddScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
builder.Services.AddHostedService<FinanceBaseRateSyncWorker>();
builder.Services.AddHostedService<PricingScheduledActivationWorker>();
await builder.Build().RunAsync();
