using EventApi.Domain.Entities;

namespace EventApi.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
