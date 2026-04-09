public class EventService : IEventService
{
    private readonly List<Event> _events = new ();
    private readonly Lock _lock = new();

    public void Add(Event eventToAdd)
    {
        using (_lock.EnterScope())
        {
            _events.Add(eventToAdd);
        }
    }

    public IEnumerable<Event> GetAll()
    {
        using (_lock.EnterScope())
        {
            return _events.ToArray();
        }
    }

    public Event? GetById(int id)
    {
        using (_lock.EnterScope())
        {
            return _events.FirstOrDefault(e => e.Id == id);
        }
    }

    public bool Remove(int id)
    {
        using (_lock.EnterScope())
        {
            if (_events.FirstOrDefault(e => e.Id == id) is not { } item)
                return false;

            return _events.Remove(item);
        }
    }

    public void Update(Event eventToUpdate)
    {
        using (_lock.EnterScope())
        {
            if (_events.FirstOrDefault(e => e.Id == eventToUpdate.Id) is not { } existingEvent)
                return;
            existingEvent.CopyFrom(eventToUpdate);
        }
    }
}
