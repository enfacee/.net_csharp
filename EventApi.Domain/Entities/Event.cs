namespace EventApi.Domain.Entities;

public class Event
{
    private Event()
    {
        Title = null!;
    }

    public Event(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats = 1)
    {
        Validate(title, startAt, endAt, totalSeats);

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
        return new Event(title, description, startAt, endAt, totalSeats);
    }

    public void UpdateDetails(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        Validate(title, startAt, endAt, totalSeats);

        var reservedSeats = TotalSeats - AvailableSeats;
        if (totalSeats < reservedSeats)
            throw new InvalidOperationException("TotalSeats cannot be less than reserved seats.");

        Title = title;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats - reservedSeats;
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

    private static void Validate(string title, DateTime startAt, DateTime endAt, int totalSeats)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (endAt <= startAt)
            throw new ArgumentException("EndAt must be greater than StartAt.", nameof(endAt));

        if (totalSeats <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSeats), "TotalSeats must be greater than 0.");
    }
}
