public interface IBookingStore
{
    bool TryAdd(Booking booking);
    IReadOnlyCollection<Booking> GetAll();
    Booking? GetById(int id);
}
