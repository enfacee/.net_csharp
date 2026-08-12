using EventApi.Users.Domain.Entities;

namespace EventApi.Users.Application.DTO;

public class RegisterRequest
{
    public string? Login { get; set; }
    public string? Password { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}
