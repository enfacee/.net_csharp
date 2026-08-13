using EventApi.Bookings.Application.DTO;
using EventApi.Bookings.Domain.Entities;

namespace EventApi.Bookings.Application.Mapping;

public static class ResponseMappingExtensions
{
    public static BookingResponse ToResponse(this Booking booking)
    {
        return new BookingResponse
        {
            Id = booking.Id,
            EventId = booking.EventId,
            UserId = booking.UserId,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            ProcessedAt = booking.ProcessedAt
        };
    }
}
