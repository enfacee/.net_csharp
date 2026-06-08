using System.Collections.Concurrent;

public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<int, Event> _events = new();

    public bool TryAdd(Event @event)
    {
        return _events.TryAdd(@event.Id, @event);
    }

    public IReadOnlyCollection<Event> GetAll()
    {
        return _events.Values.ToArray();
    }

    public Event? GetById(int id)
    {
        return _events.TryGetValue(id, out var @event)
            ? @event
            : null;
    }

    public bool TryRemove(int id)
    {
        return _events.TryRemove(id, out _);
    }

    public bool TryUpdate(Event @event)
    {
        if (!_events.TryGetValue(@event.Id, out var existingEvent))
            return false;

        existingEvent.CopyFrom(@event);
        return true;
    }
}
