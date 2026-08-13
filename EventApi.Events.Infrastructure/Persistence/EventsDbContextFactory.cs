using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventApi.Events.Infrastructure.Persistence;

internal sealed class EventsDbContextFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    public EventsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5434;Database=eventapi_events;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EventsDbContext(options);
    }
}
