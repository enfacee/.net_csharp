namespace EventApi.Domain.Exceptions;

public class EventAlreadyStartedException(string message) : Exception(message)
{
}
