using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Services;
using EventApi.Events.Application.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace EventApi.Events.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEventsApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Secret), "Jwt:Secret is not configured.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Secret) >= 32,
                "Jwt:Secret must be at least 32 bytes long.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is not configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is not configured.")
            .Validate(options => options.LifetimeMinutes > 0, "Jwt:LifetimeMinutes must be greater than 0.")
            .ValidateOnStart();

        return services;
    }
}
