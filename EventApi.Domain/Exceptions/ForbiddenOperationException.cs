namespace EventApi.Domain.Exceptions;

public class ForbiddenOperationException(string message) : Exception(message)
{
}
