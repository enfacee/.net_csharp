namespace EventApi.Shared.Contracts;

public sealed record BookingCreated(
    int BookingId,
    int EventId,
    int UserId,
    int Seats,
    DateTime CreatedAt);
