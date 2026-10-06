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

    [Fact]
    public async Task RabbitMqDrainsARealConsumerThroughputBurstWithoutDuplicates()
    {
        var host = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST")
            ?? throw new InvalidOperationException("Set TODIR_RABBITMQ_HOST for the RabbitMQ throughput gate.");
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT"), out var port) ? port : 5672,
            VirtualHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev",
            UserName = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USERNAME") ?? "todir",
            Password = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? throw new InvalidOperationException("RabbitMQ password is required for integration tests.")
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queue = $"kernel-throughput-{Guid.NewGuid():N}";
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: true);
        const int messageCount = 1_000;
        var expected = Enumerable.Range(0, messageCount).Select(_ => Guid.CreateVersion7()).ToArray();
        foreach (var messageId in expected) await channel.BasicPublishAsync(string.Empty, queue, Encoding.UTF8.GetBytes(messageId.ToString("D")));

        var received = new HashSet<Guid>();
        for (var attempt = 0; attempt < messageCount * 2 && received.Count < messageCount; attempt++)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: true);
            if (message is null)
            {
                await Task.Delay(5);
                continue;
            }
            Assert.True(Guid.TryParse(Encoding.UTF8.GetString(message.Body.ToArray()), out var messageId));
            Assert.True(received.Add(messageId), $"Duplicate consumer delivery observed for {messageId}.");
        }

        Assert.Equal(messageCount, received.Count);
    }
}
