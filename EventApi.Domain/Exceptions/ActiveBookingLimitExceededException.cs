namespace EventApi.Domain.Exceptions;

public class ActiveBookingLimitExceededException(string message) : Exception(message)
{
}
