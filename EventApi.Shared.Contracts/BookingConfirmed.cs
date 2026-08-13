namespace EventApi.Shared.Contracts;

public sealed record BookingConfirmed(
    int BookingId,
    int EventId,
    int UserId,
    int Seats,
    DateTime ConfirmedAt);
