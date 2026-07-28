using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventApi.Application.Security;
using EventApi.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EventApi.Infrastructure.Security;

public sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtOptions = options.Value;
        Validate(jwtOptions);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(jwtOptions.LifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is not configured.");

        if (Encoding.UTF8.GetByteCount(options.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes long.");

        if (string.IsNullOrWhiteSpace(options.Issuer))
            throw new InvalidOperationException("Jwt:Issuer is not configured.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Jwt:Audience is not configured.");

        if (options.LifetimeMinutes <= 0)
            throw new InvalidOperationException("Jwt:LifetimeMinutes must be greater than 0.");
    }
}
