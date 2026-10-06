using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Todir.Platform.Messaging;

public static class MessagingHostingExtensions
{
    public static IServiceCollection AddTodirRabbitMq(this IServiceCollection services, IConfiguration configuration, Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var host = configuration["TODIR_RABBITMQ_HOST"];
        if (string.IsNullOrWhiteSpace(host)) return services;
        services.AddConsumeObserver<MessageFailureObserver>();
        services.AddMassTransit(options =>
        {
            configureConsumers?.Invoke(options);
            if (bool.TryParse(configuration["TODIR_RABBITMQ_CONSUMER"], out var consumerEnabled) && consumerEnabled)
                options.AddConsumer<OutboxPublishedMessageConsumer, OutboxPublishedMessageConsumerDefinition>();
            options.AddRabbitMqConfigureEndpointsCallback((_, _, endpoint) =>
            {
                endpoint.SetQuorumQueue();
                endpoint.PrefetchCount = 16;
            });
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
                            var certificatePath = configuration["TODIR_RABBITMQ_CA_CERTIFICATE_PATH"];
                            if (!string.IsNullOrWhiteSpace(certificatePath)) ssl.CertificateValidationCallback = CreateCertificateValidationCallback(certificatePath);
                        });
                    }
                });
                bus.ConfigureEndpoints(context);
            });
        });
        return services;
    }

    private static RemoteCertificateValidationCallback CreateCertificateValidationCallback(string certificatePath)
    {
        var trustedCertificate = X509CertificateLoader.LoadCertificate(File.ReadAllBytes(certificatePath));
        return (_, certificate, _, policyErrors) =>
        {
            if (certificate is null || policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)) return false;
            using var serverCertificate = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
            if (serverCertificate.RawData.AsSpan().SequenceEqual(trustedCertificate.RawData)) return true;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(trustedCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(serverCertificate);
        };
    }
}
