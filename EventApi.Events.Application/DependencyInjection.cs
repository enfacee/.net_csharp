using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Options;
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
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventSeatReservationService, EventSeatReservationService>();
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
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConsumerGroup), "Kafka:ConsumerGroup is not configured.")
            .ValidateOnStart();
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Redis:ConnectionString is not configured.")
            .ValidateOnStart();

        return services;
    }
}
