namespace EventApi;

public class NotFoundException(string message) : Exception(message)
{
}

