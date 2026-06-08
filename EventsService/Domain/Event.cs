using System.ComponentModel.DataAnnotations;

public class Event(string title, string? description, DateTime startAt, DateTime endAt, int totalSeats = 1)
{
    private static int next;
    [Required]
    public int Id { get; private set; } = ++next;
    [Required]
    public string Title { get; set; } = title;
    public string? Description { get; set; } = description;
    [Required]
    public DateTime StartAt { get; set; } = startAt;
    [Required]
    public DateTime EndAt { get; set; } = endAt;
    [Required]
    public int TotalSeats { get; private set; } = totalSeats;
    public int AvailableSeats { get; private set; } = totalSeats;

    public static Event Create(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        if (totalSeats <= 0)
            throw new ValidationException("TotalSeats must be greater than 0.");

        return new Event(title, description, startAt, endAt, totalSeats);
    }

    public bool TryReserveSeats(int count = 1)
    {
        ValidateSeatCount(count);

        if (AvailableSeats < count)
            return false;

        AvailableSeats -= count;
        return true;
    }

    public void ReleaseSeats(int count = 1)
    {
        ValidateSeatCount(count);

        AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
    }

    private static void ValidateSeatCount(int count)
    {
        if (count <= 0)
            throw new ValidationException("Seat count must be greater than 0.");
    }
}
