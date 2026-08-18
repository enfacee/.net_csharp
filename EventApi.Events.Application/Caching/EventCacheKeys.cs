namespace EventApi.Events.Application.Caching;

public static class EventCacheKeys
{
    public const string TopEvents = "events:top10";

    public static string EventById(int id)
    {
        return $"event:{id}";
    }
}
