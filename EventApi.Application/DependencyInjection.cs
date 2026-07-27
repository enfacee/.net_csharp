using EventApi.Application.Abstractions;
using EventApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}
