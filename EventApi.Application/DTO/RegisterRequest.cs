using EventApi.Domain.Entities;

namespace EventApi.Application.DTO;

public class RegisterRequest
{
    public string? Login { get; set; }
    public string? Password { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}
