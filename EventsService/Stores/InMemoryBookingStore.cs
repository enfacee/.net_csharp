using System.Collections.Concurrent;

public class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<int, Booking> _bookings = new();

    public bool TryAdd(Booking booking)
    {
        return _bookings.TryAdd(booking.Id, booking);
    }

    public IReadOnlyCollection<Booking> GetAll()
    {
        return _bookings.Values.ToArray();
    }

    public Booking? GetById(int id)
    {
        return _bookings.TryGetValue(id, out var booking)
            ? booking
            : null;
    }
}
