using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Infrastructure.BackgroundServices;
using EventApi.Bookings.Infrastructure.Messaging;
using EventApi.Bookings.Infrastructure.Persistence;
using EventApi.Bookings.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Bookings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingsInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection connection string is not configured.");

        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddSingleton<IBookingEventPublisher, KafkaBookingEventPublisher>();
        services.AddHostedService<BookingProcessingBackgroundService>();

        return services;
    }

    public static void MigrateBookingsDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();

        if (db.Database.IsRelational())
            db.Database.Migrate();
    }
}
