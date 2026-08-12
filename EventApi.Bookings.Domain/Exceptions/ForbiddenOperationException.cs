namespace EventApi.Bookings.Domain.Exceptions;

public class ForbiddenOperationException(string message) : Exception(message);
