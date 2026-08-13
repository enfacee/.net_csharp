using EventApi.Bookings.Domain.Entities;
using EventApi.Bookings.Infrastructure.Persistence.Repositories;

namespace EventApi.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BookingRepositoryTests(PostgreSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetBookingsDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_AndGetByIdAsync_ShouldPersistBookingWithDatabaseGeneratedId()
    {
        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);
        var booking = CreateBooking(eventId: 999, userId: 123);

        await repository.AddAsync(booking);

        Assert.True(booking.Id > 0);

        var result = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(result);
        Assert.Equal(999, result.EventId);
        Assert.Equal(123, result.UserId);
        Assert.Equal(BookingStatus.Pending, result.Status);
    }

    [Fact]
    public async Task AddAsync_ShouldNotRequireForeignKeysToOtherServiceDatabases()
    {
        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);
        var booking = CreateBooking(eventId: 999999, userId: 888888);

        await repository.AddAsync(booking);

        var result = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(result);
        Assert.Equal(999999, result.EventId);
        Assert.Equal(888888, result.UserId);
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookingsOrderedById()
    {
        var pending1 = CreateBooking(1, 1);
        var confirmed = CreateBooking(1, 1);
        confirmed.Confirm(ProcessedAt);
        var pending2 = CreateBooking(1, 1);
        await SeedBookingsAsync(pending1, confirmed, pending2);

        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetPendingBookingsAsync();

        Assert.Equal([pending1.Id, pending2.Id], result.Select(booking => booking.Id).ToArray());
        Assert.All(result, booking => Assert.Equal(BookingStatus.Pending, booking.Status));
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnOnlyPendingBookingIdsOrderedById()
    {
        var pending1 = CreateBooking(1, 1);
        var rejected = CreateBooking(1, 1);
        rejected.Reject(ProcessedAt);
        var pending2 = CreateBooking(1, 1);
        await SeedBookingsAsync(pending1, rejected, pending2);

        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetPendingBookingIdsAsync();

        Assert.Equal([pending1.Id, pending2.Id], result);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistTrackedBookingChanges()
    {
        var booking = CreateBooking(1, 1);
        await SeedBookingsAsync(booking);

        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);
        var persistedBooking = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(persistedBooking);

        persistedBooking.Confirm(ProcessedAt);
        await repository.SaveChangesAsync();

        await using var assertContext = fixture.CreateBookingsContext();
        var result = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, result!.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    [Fact]
    public async Task CountActiveByUserIdAsync_ShouldCountOnlyPendingAndConfirmedBookingsForUser()
    {
        var pending = CreateBooking(1, 10);
        var confirmed = CreateBooking(2, 10);
        confirmed.Confirm(ProcessedAt);
        var rejected = CreateBooking(3, 10);
        rejected.Reject(ProcessedAt);
        var otherUserBooking = CreateBooking(4, 20);
        await SeedBookingsAsync(pending, confirmed, rejected, otherUserBooking);

        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);

        var result = await repository.CountActiveByUserIdAsync(10);

        Assert.Equal(2, result);
    }

    private async Task SeedBookingsAsync(params Booking[] bookings)
    {
        await using var context = fixture.CreateBookingsContext();
        var repository = new BookingRepository(context);

        foreach (var booking in bookings)
        {
            await repository.AddAsync(booking);
        }
    }

    private static Booking CreateBooking(int eventId, int userId)
    {
        return new Booking(eventId, userId, CreatedAt);
    }

    private static DateTime CreatedAt => new(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
    private static DateTime ProcessedAt => CreatedAt.AddMinutes(5);
}
