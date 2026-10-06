using Todir.Platform.Hosting;
using Todir.Platform.Scheduling;
using Todir.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTodirPlatform(builder.Configuration);
builder.Services.AddTodirScheduling(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchWorker>();
await builder.Build().RunAsync();
