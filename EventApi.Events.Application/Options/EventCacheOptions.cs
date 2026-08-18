namespace EventApi.Events.Application.Options;

public sealed class EventCacheOptions
{
    public const string SectionName = "EventCache";

    public int EventByIdTtlSeconds { get; set; } = 60;
    public int TopEventsTtlSeconds { get; set; } = 300;

    public TimeSpan EventByIdTtl => TimeSpan.FromSeconds(EventByIdTtlSeconds);
    public TimeSpan TopEventsTtl => TimeSpan.FromSeconds(TopEventsTtlSeconds);
}
