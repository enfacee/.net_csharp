using EventApi.Users.Domain.Entities;

namespace EventApi.Users.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
