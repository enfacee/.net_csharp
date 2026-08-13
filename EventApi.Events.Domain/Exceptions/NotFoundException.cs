namespace EventApi.Events.Domain.Exceptions;

public class NotFoundException(string message) : Exception(message);
