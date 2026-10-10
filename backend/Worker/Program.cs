using Core.Hosting;
using Core.Scheduling;
using Modules.Sample;
using Worker;

var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseReadiness.WaitForPlatformSchemaAsync(connectionString, CancellationToken.None);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRouting();
builder.Services.AddCore(builder.Configuration, SampleModuleExtensions.ConfigureSampleMessaging);
builder.Services.AddSampleModule();
builder.Services.AddScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
await builder.Build().RunAsync();
