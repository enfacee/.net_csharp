namespace EventApi.Domain.Entities;

public class User
{
    private User()
    {
        Login = null!;
        PasswordHash = null!;
    }

    public User(string login, string passwordHash, UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login is required.", nameof(login));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));

        Login = login;
        PasswordHash = passwordHash;
        Role = role;
    }

    public int Id { get; private set; }
    public string Login { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public ICollection<Booking> Bookings { get; private set; } = [];
}
