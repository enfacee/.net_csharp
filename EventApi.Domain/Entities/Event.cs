namespace EventApi.Domain.Entities;

public class Event
{
    private Event()
    {
        Title = null!;
    }

    public Event(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats = 1)
    {
        Title = title;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
    }

    public int Id { get; private set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; private set; }
    public int AvailableSeats { get; private set; }
    public ICollection<Booking> Bookings { get; private set; } = [];

    public static Event Create(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        if (totalSeats <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSeats), "TotalSeats must be greater than 0.");

        return new Event(title, description, startAt, endAt, totalSeats);
    }

    public bool TryReserveSeats(int count = 1)
    {
        ValidateSeatCount(count);

        if (AvailableSeats < count)
            return false;

        AvailableSeats -= count;
        return true;
    }

    public void ReleaseSeats(int count = 1)
    {
        ValidateSeatCount(count);

        AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
    }

    private static void ValidateSeatCount(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Seat count must be greater than 0.");
    }
}

