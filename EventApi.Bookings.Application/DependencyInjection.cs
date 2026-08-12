using System.Text;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Application.Options;
using EventApi.Bookings.Application.Security;
using EventApi.Bookings.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Bookings.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingsApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBookingService, BookingService>();
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
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka:BootstrapServers is not configured.")
            .ValidateOnStart();

        return services;
    }
}
