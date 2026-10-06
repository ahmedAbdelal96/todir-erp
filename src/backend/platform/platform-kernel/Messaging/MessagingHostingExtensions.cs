using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Authentication;

namespace Todir.Platform.Messaging;

public static class MessagingHostingExtensions
{
    public static IServiceCollection AddTodirRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["TODIR_RABBITMQ_HOST"];
        if (string.IsNullOrWhiteSpace(host)) return services;
        services.AddMassTransit(options =>
        {
            if (bool.TryParse(configuration["TODIR_RABBITMQ_CONSUMER"], out var consumerEnabled) && consumerEnabled)
                options.AddConsumer<OutboxPublishedMessageConsumer>();
            options.UsingRabbitMq((context, bus) =>
            {
                var hostAddress = new UriBuilder("rabbitmq", host)
                {
                    Path = configuration["TODIR_RABBITMQ_VHOST"] ?? "/erp-dev"
                };
                if (ushort.TryParse(configuration["TODIR_RABBITMQ_PORT"], out var port)) hostAddress.Port = port;
                bus.Host(hostAddress.Uri, hostSettings =>
                {
                    hostSettings.Username(configuration["TODIR_RABBITMQ_USER"] ?? configuration["TODIR_RABBITMQ_USERNAME"] ?? "todir");
                    hostSettings.Password(configuration["TODIR_RABBITMQ_PASSWORD"] ?? throw new InvalidOperationException("RabbitMQ password must be supplied by the secret manager."));
                    hostSettings.PublisherConfirmation = true;
                    if (bool.TryParse(configuration["TODIR_RABBITMQ_TLS"], out var tlsEnabled) && tlsEnabled)
                    {
                        hostSettings.UseSsl(ssl =>
                        {
                            ssl.Protocol = SslProtocols.Tls12;
                            ssl.ServerName = configuration["TODIR_RABBITMQ_TLS_SERVER_NAME"] ?? host;
                        });
                    }
                });
                bus.ConfigureEndpoints(context);
            });
        });
        return services;
    }
}
