using EventApi.Domain.Entities;
using EventApi.Infrastructure.Persistence;
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
    public async Task AddAsync_UnitOfWorkSaveChangesAsync_GetByIdAsync_AndExistsAsync_ShouldPersistBookingWithDatabaseGeneratedId()
    {
        var @event = await SeedEventAsync();
        var user = await SeedUserAsync();
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var unitOfWork = new EfUnitOfWork(context);
        var booking = CreateBooking(@event.Id, user.Id);

        await repository.AddAsync(booking);
        await unitOfWork.SaveChangesAsync();

        Assert.True(booking.Id > 0);
        Assert.True(await repository.ExistsAsync(booking.Id));
        Assert.False(await repository.ExistsAsync(booking.Id + 1));

        var result = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(result);
        Assert.Equal(@event.Id, result.EventId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(BookingStatus.Pending, result.Status);
    }

    [Fact]
    public async Task AddAsync_ShouldUseDatabaseForeignKey()
    {
        var user = await SeedUserAsync();
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var unitOfWork = new EfUnitOfWork(context);

        await repository.AddAsync(CreateBooking(eventId: 999999, user.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookingsOrderedById()
    {
        var @event = await SeedEventAsync(totalSeats: 3);
        var user = await SeedUserAsync();
        var pending1 = CreateBooking(@event.Id, user.Id);
        var confirmed = CreateBooking(@event.Id, user.Id);
        confirmed.Confirm(ProcessedAt);
        var pending2 = CreateBooking(@event.Id, user.Id);
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
        var user = await SeedUserAsync();
        var pending1 = CreateBooking(@event.Id, user.Id);
        var rejected = CreateBooking(@event.Id, user.Id);
        rejected.Reject(ProcessedAt);
        var pending2 = CreateBooking(@event.Id, user.Id);
        await SeedBookingsAsync(pending1, rejected, pending2);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetPendingBookingIdsAsync();

        Assert.Equal([pending1.Id, pending2.Id], result);
    }

    [Fact]
    public async Task UnitOfWorkSaveChangesAsync_ShouldPersistTrackedBookingChanges()
    {
        var @event = await SeedEventAsync();
        var user = await SeedUserAsync();
        var booking = CreateBooking(@event.Id, user.Id);
        await SeedBookingsAsync(booking);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var unitOfWork = new EfUnitOfWork(context);
        var persistedBooking = await repository.GetByIdAsync(booking.Id);
        Assert.NotNull(persistedBooking);

        persistedBooking.Confirm(ProcessedAt);
        await unitOfWork.SaveChangesAsync();

        await using var assertContext = fixture.CreateContext();
        var result = await new BookingRepository(assertContext).GetByIdAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, result!.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    [Fact]
    public async Task CountActiveByUserIdAsync_ShouldCountOnlyPendingAndConfirmedBookingsForUser()
    {
        var @event = await SeedEventAsync(totalSeats: 5);
        var user = await SeedUserAsync("active-count-user");
        var otherUser = await SeedUserAsync("other-active-count-user");
        var pending = CreateBooking(@event.Id, user.Id);
        var confirmed = CreateBooking(@event.Id, user.Id);
        confirmed.Confirm(ProcessedAt);
        var rejected = CreateBooking(@event.Id, user.Id);
        rejected.Reject(ProcessedAt);
        var otherUserBooking = CreateBooking(@event.Id, otherUser.Id);
        await SeedBookingsAsync(pending, confirmed, rejected, otherUserBooking);

        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.CountActiveByUserIdAsync(user.Id);

        Assert.Equal(2, result);
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
        var unitOfWork = new EfUnitOfWork(context);
        var startAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);
        var @event = new Event("Repository test event", null, startAt, startAt.AddHours(1), totalSeats);

        await repository.AddAsync(@event);
        await unitOfWork.SaveChangesAsync();

        return @event;
    }

    private async Task SeedBookingsAsync(params Booking[] bookings)
    {
        await using var context = fixture.CreateContext();
        var repository = new BookingRepository(context);
        var unitOfWork = new EfUnitOfWork(context);

        foreach (var booking in bookings)
        {
            await repository.AddAsync(booking);
        }

        await unitOfWork.SaveChangesAsync();
    }

    private async Task<User> SeedUserAsync(string login = "repository-user")
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var unitOfWork = new EfUnitOfWork(context);
        var user = new User(login, "HASH", UserRole.User);

        await repository.AddAsync(user);
        await unitOfWork.SaveChangesAsync();

        return user;
    }

    private static Booking CreateBooking(int eventId, int userId)
    {
        return new Booking(eventId, userId, CreatedAt);
    }

    private static DateTime CreatedAt => new(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
    private static DateTime ProcessedAt => CreatedAt.AddMinutes(5);
}
