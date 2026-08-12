using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventApi.Users.Application.Abstractions;
using EventApi.Users.Application.Security;
using EventApi.Users.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EventApi.Users.Infrastructure.Security;

public sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtTokenGenerator
{
    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtOptions = options.Value;
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
}
