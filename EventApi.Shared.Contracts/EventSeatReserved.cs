namespace EventApi.Shared.Contracts;

public sealed record EventSeatReserved(
    int BookingId,
    int EventId,
    int UserId,
    int Seats,
    DateTime ReservedAt);
