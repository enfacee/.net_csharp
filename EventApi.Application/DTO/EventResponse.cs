namespace EventApi.Application.DTO;

public class EventResponse
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
}

