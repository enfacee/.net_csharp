using System.ComponentModel.DataAnnotations;

public class Booking(int eventId)
{
    private static int next;

    [Required]
    public int Id { get; private set; } = ++next;

    [Required]
    public int EventId { get; set; } = eventId;

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
