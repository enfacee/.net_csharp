using System.Text.Json;
using Confluent.Kafka;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Application.Options;
using EventApi.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace EventApi.Bookings.Infrastructure.Messaging;

public sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;

    public KafkaBookingEventPublisher(IOptions<KafkaOptions> options)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishBookingCreatedAsync(
        BookingCreated message,
        CancellationToken cancellationToken = default)
    {
        await ProduceAsync(KafkaTopics.BookingCreated, message.EventId, message, cancellationToken);
    }

    public async Task PublishBookingConfirmedAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        await ProduceAsync(KafkaTopics.BookingConfirmed, message.EventId, message, cancellationToken);
    }

    public async Task PublishBookingRejectedAsync(
        BookingRejected message,
        CancellationToken cancellationToken = default)
    {
        await ProduceAsync(KafkaTopics.BookingRejected, message.EventId, message, cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }

    private async Task ProduceAsync<T>(
        string topic,
        int eventId,
        T message,
        CancellationToken cancellationToken)
    {
        var kafkaMessage = new Message<string, string>
        {
            Key = eventId.ToString(),
            Value = JsonSerializer.Serialize(message, JsonOptions)
        };

        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }
}
