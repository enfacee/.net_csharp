using EventApi.Events.Application.Abstractions;
using EventApi.Shared.Contracts;

namespace EventApi.Events.Application.Services;

public sealed class EventSeatReservationService(
    IEventRepository eventRepository,
    IEventSeatReservationPublisher publisher,
    TimeProvider timeProvider,
    IEventReadCache readCache) : IEventSeatReservationService
{
    public async Task HandleBookingCreatedAsync(
        BookingCreated message,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var @event = await eventRepository.GetByIdAsync(message.EventId, cancellationToken);

        if (@event is null)
        {
            await PublishUnavailableAsync(message, "Event not found.", now, cancellationToken);
            return;
        }

        if (@event.StartAt <= now)
        {
            await PublishUnavailableAsync(message, "Event has already started.", now, cancellationToken);
            return;
        }

        if (!@event.TryReserveSeats(message.Seats))
        {
            await PublishUnavailableAsync(message, "No available seats.", now, cancellationToken);
            return;
        }

        await eventRepository.SaveChangesAsync(cancellationToken);
        await readCache.RemoveEventAsync(message.EventId, cancellationToken);
        await publisher.PublishSeatReservedAsync(
            new EventSeatReserved(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.Seats,
                ReservedAt: now),
            cancellationToken);
    }

    private async Task PublishUnavailableAsync(
        BookingCreated message,
        string reason,
        DateTime rejectedAt,
        CancellationToken cancellationToken)
    {
        await publisher.PublishSeatUnavailableAsync(
            new EventSeatUnavailable(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.Seats,
                reason,
                rejectedAt),
            cancellationToken);
    }
}
