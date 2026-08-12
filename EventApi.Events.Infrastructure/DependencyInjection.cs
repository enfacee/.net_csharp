using EventApi.Events.Application.Abstractions;
using EventApi.Events.Infrastructure.Persistence;
using EventApi.Events.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEventsInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection connection string is not configured.");

        services.AddDbContext<EventsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IEventRepository, EventRepository>();

        return services;
    }

    public static void MigrateEventsDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        if (db.Database.IsRelational())
            db.Database.Migrate();
    }
}
