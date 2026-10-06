using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Todir.Platform.Messaging;

public static class MessagingHostingExtensions
{
    public static IServiceCollection AddTodirRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["TODIR_RABBITMQ_HOST"];
        if (string.IsNullOrWhiteSpace(host)) return services;
        services.AddMassTransit(options => options.UsingRabbitMq((context, bus) =>
        {
            bus.Host(host, configuration["TODIR_RABBITMQ_VHOST"] ?? "/erp-dev", hostSettings =>
            {
            hostSettings.Username(configuration["TODIR_RABBITMQ_USER"] ?? configuration["TODIR_RABBITMQ_USERNAME"] ?? "todir");
                hostSettings.Password(configuration["TODIR_RABBITMQ_PASSWORD"] ?? throw new InvalidOperationException("RabbitMQ password must be supplied by the secret manager."));
            });
            bus.ConfigureEndpoints(context);
        }));
        return services;
    }
}
