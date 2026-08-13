using System.Text.Json;
using Confluent.Kafka;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Application.Options;
using EventApi.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventApi.Bookings.Infrastructure.Messaging;

public sealed class EventSeatReservationResultConsumerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<EventSeatReservationResultConsumerBackgroundService> logger) : BackgroundService
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
        consumer.Subscribe([KafkaTopics.EventSeatReserved, KafkaTopics.EventSeatUnavailable]);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = consumer.Consume(stoppingToken);
                    await HandleMessageAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "Failed to consume event seat reservation result message.");
                }
                catch (JsonException exception)
                {
                    logger.LogError(exception, "Failed to deserialize event seat reservation result message.");

                    if (result is not null)
                        consumer.Commit(result);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to handle event seat reservation result message.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Event seat reservation result consumer stopped.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleMessageAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        if (result.Topic == KafkaTopics.EventSeatReserved)
        {
            var message = JsonSerializer.Deserialize<EventSeatReserved>(result.Message.Value, JsonOptions);
            if (message is null)
            {
                logger.LogWarning("Skipping empty EventSeatReserved message at {TopicPartitionOffset}.", result.TopicPartitionOffset);
                return;
            }

            await bookingService.ConfirmBookingAsync(message.BookingId, cancellationToken);
            return;
        }

        if (result.Topic == KafkaTopics.EventSeatUnavailable)
        {
            var message = JsonSerializer.Deserialize<EventSeatUnavailable>(result.Message.Value, JsonOptions);
            if (message is null)
            {
                logger.LogWarning("Skipping empty EventSeatUnavailable message at {TopicPartitionOffset}.", result.TopicPartitionOffset);
                return;
            }

            await bookingService.RejectBookingAsync(message.BookingId, cancellationToken);
            return;
        }

        logger.LogWarning("Skipping message from unexpected topic {Topic}.", result.Topic);
    }
}
