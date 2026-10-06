using System.Text;
using RabbitMQ.Client;
using Xunit;

namespace KernelIntegrationTests;

public sealed class BrokerIntegrationTests
{
    [Fact]
    public async Task RabbitMqAcceptsAndReturnsARealMessage()
    {
        var host = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST")
            ?? throw new InvalidOperationException("Set TODIR_RABBITMQ_HOST for the RabbitMQ integration gate.");
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT"), out var port) ? port : 5672,
            VirtualHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev",
            UserName = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USERNAME") ?? "todir",
            Password = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? throw new InvalidOperationException("RabbitMQ password is required for integration tests."),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queue = $"kernel-verification-{Guid.NewGuid():N}";
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: true);
        var payload = Encoding.UTF8.GetBytes("traceparent=verification;tenant=integration");
        await channel.BasicPublishAsync(string.Empty, queue, payload);
        var message = await channel.BasicGetAsync(queue, autoAck: true);
        Assert.NotNull(message);
        Assert.Equal(payload, message!.Body.ToArray());
    }
}
