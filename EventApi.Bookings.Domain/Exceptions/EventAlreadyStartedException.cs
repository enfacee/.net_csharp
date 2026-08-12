namespace EventApi.Bookings.Domain.Exceptions;

public class EventAlreadyStartedException(string message) : Exception(message);
