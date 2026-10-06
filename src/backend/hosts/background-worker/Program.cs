using Todir.Platform.Hosting;
using Todir.Platform.Scheduling;
using Todir.Modules.Sample;
using Todir.Worker;

var connectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
if (!string.IsNullOrWhiteSpace(connectionString))
    await DatabaseReadiness.WaitForPlatformSchemaAsync(connectionString, CancellationToken.None);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddRouting();
builder.Services.AddTodirPlatform(builder.Configuration, SampleModuleExtensions.ConfigureSampleMessaging);
builder.Services.AddSampleModule();
builder.Services.AddTodirScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
await builder.Build().RunAsync();
