namespace EventApi.Shared.Contracts;

public static class KafkaTopics
{
    public const string BookingCreated = "booking-created";
    public const string EventSeatReserved = "event-seat-reserved";
    public const string EventSeatUnavailable = "event-seat-unavailable";
    public const string BookingConfirmed = "booking-confirmed";
    public const string BookingRejected = "booking-rejected";
}
