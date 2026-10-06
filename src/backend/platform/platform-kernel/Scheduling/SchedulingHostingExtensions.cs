using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Todir.Platform.Scheduling;

public static class SchedulingHostingExtensions
{
    public static IServiceCollection AddTodirScheduling(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Todir") ?? configuration["TODIR_DATABASE_CONNECTION"];
        services.AddQuartz(options =>
        {
            options.SchedulerId = "TodirScheduler";
            options.SchedulerName = "TODIR persistent scheduler";
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
                });
            }
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        return services;
    }
}
