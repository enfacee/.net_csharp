using System.Text;
using EventApi.Application.Abstractions;
using EventApi.Application.Security;
using EventApi.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationServices();
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
