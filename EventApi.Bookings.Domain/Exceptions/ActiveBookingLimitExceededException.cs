namespace EventApi.Bookings.Domain.Exceptions;

public class ActiveBookingLimitExceededException(string message) : Exception(message);
