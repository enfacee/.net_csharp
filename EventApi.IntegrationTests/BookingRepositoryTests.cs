using EventApi.Domain.Entities;
using EventApi.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_SaveChangesAsync_GetByIdAsync_AndExistsAsync_ShouldPersistBookingWithDatabaseGeneratedId()
    {
        var @event = await SeedEventAsync();
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var booking = CreateBooking(@event.Id);

        await repository.AddAsync(booking);
        await repository.SaveChangesAsync();

        Assert.True(booking.Id > 0);
        Assert.True(await repository.ExistsAsync(booking.Id));
        Assert.False(await repository.ExistsAsync(booking.Id + 1));

        var result = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(result);
        Assert.Equal(@event.Id, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
    }

    [Fact]
    public async Task AddAsync_ShouldUseDatabaseForeignKey()
    {
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        await repository.AddAsync(CreateBooking(eventId: 999999));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookingsOrderedById()
    {
        var @event = await SeedEventAsync(totalSeats: 3);
        var pending1 = CreateBooking(@event.Id);
        var confirmed = CreateBooking(@event.Id);
        confirmed.Confirm(ProcessedAt);
        var pending2 = CreateBooking(@event.Id);
        await SeedBookingsAsync(pending1, confirmed, pending2);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetPendingBookingsAsync();

        Assert.Equal([pending1.Id, pending2.Id], result.Select(booking => booking.Id).ToArray());
        Assert.All(result, booking => Assert.Equal(BookingStatus.Pending, booking.Status));
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnOnlyPendingBookingIdsOrderedById()
    {
        var @event = await SeedEventAsync(totalSeats: 3);
        var pending1 = CreateBooking(@event.Id);
        var rejected = CreateBooking(@event.Id);
        rejected.Reject(ProcessedAt);
        var pending2 = CreateBooking(@event.Id);
        await SeedBookingsAsync(pending1, rejected, pending2);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetPendingBookingIdsAsync();

        Assert.Equal([pending1.Id, pending2.Id], result);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistTrackedBookingChanges()
    {
        var @event = await SeedEventAsync();
        var booking = CreateBooking(@event.Id);
        await SeedBookingsAsync(booking);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var persistedBooking = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(persistedBooking);

        persistedBooking.Confirm(ProcessedAt);
        await repository.SaveChangesAsync();

        await using var assertContext = fixture.CreateContext();
        var result = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, result!.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullForMissingBooking()
    {
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetByIdAsync(999999);

        Assert.Null(result);
    }

    private async Task<Event> SeedEventAsync(int totalSeats = 10)
    {
        await using var context = fixture.CreateContext();
        var repository = new EventRepository(context);
        var startAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);
        var @event = new Event("Repository test event", null, startAt, startAt.AddHours(1), totalSeats);

        await repository.AddAsync(@event);
        await repository.SaveChangesAsync();

        return @event;
    }

    private async Task SeedBookingsAsync(params Booking[] bookings)
    {
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        foreach (var booking in bookings)
        {
            await repository.AddAsync(booking);
        }

        await repository.SaveChangesAsync();
    }

    private static Booking CreateBooking(int eventId)
    {
        return new Booking(eventId, CreatedAt);
    }

    private static DateTime CreatedAt => new(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
    private static DateTime ProcessedAt => CreatedAt.AddMinutes(5);
}
