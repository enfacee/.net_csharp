public interface IEventService
{
    Task<Event> CreateEventAsync(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats);
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
