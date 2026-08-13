using EventApi.Events.Application.DTO;
using EventApi.Events.Domain.Entities;

namespace EventApi.Events.Application.Mapping;

public static class ResponseMappingExtensions
{
    public static EventResponse ToResponse(this Event @event)
    {
        return new EventResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Description = @event.Description,
            StartAt = @event.StartAt,
            EndAt = @event.EndAt,
            TotalSeats = @event.TotalSeats,
            AvailableSeats = @event.AvailableSeats
        };
    }
}
