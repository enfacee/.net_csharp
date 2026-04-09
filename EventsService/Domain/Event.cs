using System.ComponentModel.DataAnnotations;

public class Event(string title, string? description, DateTime startAt, DateTime endAt)
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
}