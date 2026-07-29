namespace EventApi.Domain.Entities;

public class Booking
{
    private Booking()
    {
        Event = null!;
        User = null!;
    }

    public Booking(int eventId, int userId, DateTime createdAt)
    {
        if (eventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventId), "EventId must be greater than 0.");

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "UserId must be greater than 0.");

        EventId = eventId;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }
    public int EventId { get; private set; }
    public Event Event { get; private set; } = null!;
    public int UserId { get; private set; }
    public User User { get; private set; } = null!;
    public BookingStatus Status { get; private set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    public void Confirm(DateTime processedAt)
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = processedAt;
    }

    public void Reject(DateTime processedAt)
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = processedAt;
    }

    public void Cancel(DateTime cancelledAt)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidOperationException("Booking is already cancelled.");

        Status = BookingStatus.Cancelled;
        ProcessedAt = cancelledAt;
    }
}
