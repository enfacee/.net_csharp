public interface IEventService
{
    PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10);
    Event? GetById(int id);
    void Add(Event eventToAdd);    
    void Update(Event eventToUpdate);
    bool Remove(int id);
}
