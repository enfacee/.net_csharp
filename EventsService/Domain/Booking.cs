using System.ComponentModel.DataAnnotations;

public class Booking(
    int eventId,
    BookingStatus status = BookingStatus.Pending,
    DateTime? createdAt = null,
    DateTime? processedAt = null)
{
    private static int next;

    [Required]
    public int Id { get; private set; } = ++next;

    [Required]
    public int EventId { get; set; } = eventId;

    [Required]
    public BookingStatus Status { get; set; } = status;

    [Required]
    public DateTime CreatedAt { get; set; } = createdAt ?? DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; } = processedAt;
}
