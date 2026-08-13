namespace EventApi.Events.Application.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = "events-service";
}
