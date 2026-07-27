namespace EventApi.Domain.Entities;

public class Booking
{
    private Booking()
    {
        Event = null!;
    }

    public Booking(int eventId)
    {
        EventId = eventId;
    }

    public int Id { get; private set; }

    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }
}

