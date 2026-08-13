namespace EventApi.Shared.Contracts;

public sealed record BookingRejected(
    int BookingId,
    int EventId,
    int UserId,
    int Seats,
    string Reason,
    DateTime RejectedAt);
