using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventApi.Bookings.Infrastructure.Persistence;

internal sealed class BookingsDbContextFactory : IDesignTimeDbContextFactory<BookingsDbContext>
{
    public BookingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5435;Database=eventapi_bookings;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BookingsDbContext(options);
    }
}
