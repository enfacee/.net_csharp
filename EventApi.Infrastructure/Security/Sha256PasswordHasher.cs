using System.Security.Cryptography;
using System.Text;

namespace EventApi.Infrastructure.Security;

public sealed class Sha256PasswordHasher
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
