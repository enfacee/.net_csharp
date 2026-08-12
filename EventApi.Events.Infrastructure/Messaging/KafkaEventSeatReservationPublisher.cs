using System.Text.Json;
using Confluent.Kafka;
using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Options;
using EventApi.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace EventApi.Events.Infrastructure.Messaging;

public sealed class KafkaEventSeatReservationPublisher : IEventSeatReservationPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<string, string> _producer;

    public KafkaEventSeatReservationPublisher(IOptions<KafkaOptions> options)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishSeatReservedAsync(
        EventSeatReserved message,
        CancellationToken cancellationToken = default)
    {
        await ProduceAsync(KafkaTopics.EventSeatReserved, message.EventId, message, cancellationToken);
    }

    public async Task PublishSeatUnavailableAsync(
        EventSeatUnavailable message,
        CancellationToken cancellationToken = default)
    {
        await ProduceAsync(KafkaTopics.EventSeatUnavailable, message.EventId, message, cancellationToken);
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
