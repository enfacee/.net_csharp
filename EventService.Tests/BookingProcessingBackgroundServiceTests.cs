using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventsService.Tests;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ProcessPendingBookingsAsync_ShouldRejectBooking_WhenEventWasDeleted()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var seedContext = CreateContext(databaseName);
        var booking = new Booking(eventId: 999999);
        seedContext.Bookings.Add(booking);
        await seedContext.SaveChangesAsync();
        var service = CreateService(databaseName);

        await ProcessPendingBookingsAsync(service);

        await using var assertContext = CreateContext(databaseName);
        var result = await assertContext.Bookings.FindAsync(booking.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBookingsAsync_ShouldRunExternalDelayInParallel()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var seedContext = CreateContext(databaseName);

        for (var i = 0; i < 3; i++)
        {
            var @event = CreateTestEvent(totalSeats: 1);
            seedContext.Events.Add(@event);
            seedContext.Bookings.Add(new Booking(@event.Id));
        }

        await seedContext.SaveChangesAsync();
        var service = CreateService(databaseName);
        var stopwatch = Stopwatch.StartNew();

        await ProcessPendingBookingsAsync(service);

        stopwatch.Stop();
        await using var assertContext = CreateContext(databaseName);
        var bookings = await assertContext.Bookings.ToArrayAsync();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
        bookings.Should().OnlyContain(booking => booking.Status == BookingStatus.Confirmed);
    }

    private static BookingProcessingBackgroundService CreateService(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider();

        return new BookingProcessingBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);
    }

    private static async Task ProcessPendingBookingsAsync(BookingProcessingBackgroundService service)
    {
        var method = typeof(BookingProcessingBackgroundService).GetMethod(
            "ProcessPendingBookingsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(service, [CancellationToken.None])!;
        await task;
    }

    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    private static Event CreateTestEvent(int totalSeats)
    {
        var startAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        return new Event("Background test event", null, startAt, startAt.AddHours(1), totalSeats);
    }
}
