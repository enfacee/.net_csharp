namespace EventApi;

public class NoAvailableSeatsException(string message) : Exception(message)
{
}

