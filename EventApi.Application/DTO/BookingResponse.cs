using EventApi.Domain.Entities;

namespace EventApi.Application.DTO;

public class BookingResponse
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

