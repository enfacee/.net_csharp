public interface IEventService
{
    PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10);
    Event? GetById(int id);
    void Add(Event @event);    
    void Update(Event @event);
    bool Remove(int id);
}
