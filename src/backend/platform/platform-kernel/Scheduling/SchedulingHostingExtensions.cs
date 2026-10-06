using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Todir.Platform.Security;

namespace Todir.Platform.Scheduling;

public static class SchedulingHostingExtensions
{
    public static IServiceCollection AddTodirScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Todir") ?? configuration["TODIR_DATABASE_CONNECTION"];
        var maintenanceIntervalSeconds = int.TryParse(configuration["TODIR_MAINTENANCE_INTERVAL_SECONDS"], out var configuredInterval) && configuredInterval > 0 ? configuredInterval : 300;
        services.AddSingleton<IPlatformMaintenanceTask, PlatformMaintenanceTask>();
        services.AddQuartz(options =>
        {
            options.SchedulerId = "TodirScheduler";
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
                        postgres.TablePrefix = "platform_scheduling.qrtz_";
                    });
                    store.UseClustering();
                });
            }
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }
}
