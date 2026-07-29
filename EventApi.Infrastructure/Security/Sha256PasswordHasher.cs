using System.Security.Cryptography;
using System.Text;
using EventApi.Application.Abstractions;

namespace EventApi.Infrastructure.Security;

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(passwordHash);

        return string.Equals(Hash(password), passwordHash, StringComparison.OrdinalIgnoreCase);
    }
}
