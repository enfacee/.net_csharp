using System.ComponentModel.DataAnnotations;

public class Booking
{
    private static int next;

    private Booking()
    {
        Event = null!;
    }

    public Booking(int eventId)
    {
        Id = ++next;
        EventId = eventId;
    }

    [Required]
    public int Id { get; private set; }

    [Required]
    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    [Required]
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [Required]
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
