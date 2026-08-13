using System.Text.Json;
using Confluent.Kafka;
using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Options;
using EventApi.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventApi.Events.Infrastructure.Messaging;

public sealed class BookingCreatedConsumerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<BookingCreatedConsumerBackgroundService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = options.Value.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(KafkaTopics.BookingCreated);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(stoppingToken);
                    var message = JsonSerializer.Deserialize<BookingCreated>(result.Message.Value, JsonOptions);
                    if (message is null)
                    {
                        logger.LogWarning("Skipping empty BookingCreated message at {TopicPartitionOffset}.", result.TopicPartitionOffset);
                        consumer.Commit(result);
                        continue;
                    }

                    using var scope = scopeFactory.CreateScope();
                    var reservationService = scope.ServiceProvider.GetRequiredService<IEventSeatReservationService>();
                    await reservationService.HandleBookingCreatedAsync(message, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "Failed to consume BookingCreated message.");
                }
                catch (JsonException exception)
                {
                    logger.LogError(exception, "Failed to deserialize BookingCreated message.");

                    if (result is not null)
                        consumer.Commit(result);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to handle BookingCreated message.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("BookingCreated consumer stopped.");
        }
        finally
        {
            consumer.Close();
        }
    }
}
