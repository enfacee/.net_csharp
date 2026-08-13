using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EventApi.Events.Application.Options;
using EventApi.Shared.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventApi.Events.Infrastructure.Messaging;

public sealed class KafkaTopicInitializerHostedService(
    IOptions<KafkaOptions> options,
    ILogger<KafkaTopicInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = options.Value.BootstrapServers
        };

        using var adminClient = new AdminClientBuilder(adminConfig).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification { Name = KafkaTopics.BookingCreated, NumPartitions = 1, ReplicationFactor = 1 },
                    new TopicSpecification { Name = KafkaTopics.EventSeatReserved, NumPartitions = 1, ReplicationFactor = 1 },
                    new TopicSpecification { Name = KafkaTopics.EventSeatUnavailable, NumPartitions = 1, ReplicationFactor = 1 },
                    new TopicSpecification { Name = KafkaTopics.BookingConfirmed, NumPartitions = 1, ReplicationFactor = 1 },
                    new TopicSpecification { Name = KafkaTopics.BookingRejected, NumPartitions = 1, ReplicationFactor = 1 }
                ],
                new CreateTopicsOptions { OperationTimeout = TimeSpan.FromSeconds(10) });
        }
        catch (CreateTopicsException exception)
        {
            if (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.LogInformation("Kafka topics already exist.");
                return;
            }

            logger.LogWarning(exception, "Failed to create one or more Kafka topics.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to initialize Kafka topics.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
