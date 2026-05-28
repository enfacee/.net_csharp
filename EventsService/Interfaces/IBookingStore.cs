public interface IBookingStore
{
    bool TryAdd(Booking booking);
    IReadOnlyCollection<Booking> GetAll();
    IReadOnlyCollection<Booking> GetPending();
    Booking? GetById(int id);
    bool TryUpdate(Booking booking);
}
