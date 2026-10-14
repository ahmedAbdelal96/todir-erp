using Core.Hosting;
using Core.Scheduling;
using Modules.Sample;
using Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddRepositoryConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName);
var connectionString = builder.Configuration.GetRuntimeConnectionString();
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseReadiness.WaitForPlatformSchemaAsync(connectionString, CancellationToken.None);

builder.Services.AddRouting();
builder.Services.AddCore(builder.Configuration, SampleModuleExtensions.ConfigureSampleMessaging);
builder.Services.AddSampleModule();
builder.Services.AddScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
await builder.Build().RunAsync();
