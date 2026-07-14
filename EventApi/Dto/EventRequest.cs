using System.ComponentModel.DataAnnotations;

namespace EventApi;

public class EventRequest : IValidatableObject
{
    public string? Title { get; set; } 
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int? TotalSeats { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title))
            yield return new ValidationResult("Title is required", [nameof(Title)]);

        if (EndAt <= StartAt)
            yield return new ValidationResult(
                "EndAt must be greater than StartAt",
                [nameof(StartAt), nameof(EndAt)]);

        if (TotalSeats is null)
            yield return new ValidationResult("TotalSeats is required", [nameof(TotalSeats)]);
        else if (TotalSeats <= 0)
            yield return new ValidationResult("TotalSeats must be greater than 0", [nameof(TotalSeats)]);
    }
}

