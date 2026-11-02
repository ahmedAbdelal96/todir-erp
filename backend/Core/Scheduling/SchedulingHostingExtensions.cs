using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Core.Security;
using Core.Hosting;

namespace Core.Scheduling;

public static class SchedulingHostingExtensions
{
    public static IServiceCollection AddScheduling(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollectionQuartzConfigurator>? configureAdditionalJobs = null)
    {
        var connectionString = configuration.GetRuntimeConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString) && int.TryParse(configuration["TODIR_DB_MAX_POOL_SIZE"], out var maxPoolSize) && maxPoolSize > 0)
        {
            var boundedConnection = new Npgsql.NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize };
            connectionString = boundedConnection.ConnectionString;
        }
        var maintenanceIntervalSeconds = int.TryParse(configuration["TODIR_MAINTENANCE_INTERVAL_SECONDS"], out var configuredInterval) && configuredInterval > 0 ? configuredInterval : 300;
        services.AddSingleton<IPlatformMaintenanceTask, PlatformMaintenanceTask>();
        services.AddQuartz(options =>
        {
            options.SetProperty("quartz.serializer.type", "newtonsoft");
            options.SchedulerId = "CoreScheduler";
            options.SchedulerName = "TODIR persistent scheduler";
            options.AddJob<PlatformMaintenanceJob>(job => job
                .WithIdentity(PlatformMaintenanceJob.Key)
                .StoreDurably()
                .RequestRecovery());
            options.AddTrigger(trigger => trigger
                .ForJob(PlatformMaintenanceJob.Key)
                .WithIdentity(PlatformMaintenanceJob.Trigger)
                .StartNow()
                .WithSimpleSchedule(schedule => schedule
                    .WithIntervalInSeconds(maintenanceIntervalSeconds)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionFireNow()));
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UsePersistentStore(store =>
                {
                    store.UseProperties = true;
                    store.UsePostgres(postgres =>
                    {
                        postgres.ConnectionString = connectionString;
                        postgres.TablePrefix = "platform.qrtz_";
                    });
                    store.UseClustering();
                });
            }
            configureAdditionalJobs?.Invoke(options);
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }
}
