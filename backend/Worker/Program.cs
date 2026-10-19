using Core.Hosting;
using Core.Scheduling;
using Modules.Sample;
using MassTransit;
using Worker;
using Modules.Catalog;

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
builder.Services.AddScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
await builder.Build().RunAsync();
