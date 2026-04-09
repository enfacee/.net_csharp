public interface IEventService
{
    IEnumerable<Event> GetAll();
    Event? GetById(int id);
    void Add(Event eventToAdd);    
    void Update(Event eventToUpdate);
    bool Remove(int id);
}