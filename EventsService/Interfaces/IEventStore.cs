public interface IEventStore
{
    bool TryAdd(Event @event);
    IReadOnlyCollection<Event> GetAll();
    Event? GetById(int id);
    bool TryRemove(int id);
    bool TryUpdate(Event @event);
}
