namespace EventApi.Shared.Contracts;

public sealed record EventSeatUnavailable(
    int BookingId,
    int EventId,
    int UserId,
    int Seats,
    string Reason,
    DateTime RejectedAt);
