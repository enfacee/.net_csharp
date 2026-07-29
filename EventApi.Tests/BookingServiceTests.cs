using System.ComponentModel.DataAnnotations;
using EventApi.Application.Abstractions;
using EventApi.Application.Services;
using EventApi.Domain.Entities;
using EventApi.Domain.Exceptions;
using EventApi.Infrastructure.Persistence;
using EventApi.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Tests;

public class BookingServiceTests : IDisposable
{
    private const int UserId = 1;
    private const int OtherUserId = 2;

    private readonly ServiceProvider _serviceProvider;

    public BookingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreatePendingBooking_WhenEventExists()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context);
        var beforeCreate = DateTime.UtcNow;

        var booking = await service.CreateBookingAsync(@event.Id, UserId);

        var afterCreate = DateTime.UtcNow;
        booking.Id.Should().BeGreaterThan(0);
        booking.EventId.Should().Be(@event.Id);
        booking.UserId.Should().Be(UserId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        booking.CreatedAt.Should().BeOnOrBefore(afterCreate);
        booking.ProcessedAt.Should().BeNull();
        @event.AvailableSeats.Should().Be(@event.TotalSeats - 1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        Func<Task> act = () => service.CreateBookingAsync(999999, UserId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event not found*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNoAvailableSeatsException_WhenEventHasNoAvailableSeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context, totalSeats: 1);
        await service.CreateBookingAsync(@event.Id, UserId);

        Func<Task> act = () => service.CreateBookingAsync(@event.Id, UserId);

        await act.Should().ThrowAsync<NoAvailableSeatsException>()
            .WithMessage("No available seats for this event");
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookings()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context);
        var pendingBooking = await service.CreateBookingAsync(@event.Id, UserId);
        var confirmedBooking = await service.CreateBookingAsync(@event.Id, UserId);
        await service.UpdateBookingStatusAsync(confirmedBooking.Id, BookingStatus.Confirmed);

        var result = await service.GetPendingBookingsAsync();

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(pendingBooking.Id);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldRejectPendingBookingAndReleaseSeat()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context);
        var booking = await service.CreateBookingAsync(@event.Id, UserId);
        var availableSeatsBeforeReject = @event.AvailableSeats;

        var result = await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected);

        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
        @event.AvailableSeats.Should().Be(availableSeatsBeforeReject + 1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldAllowOnlyAvailableSeats_WhenCalledConcurrently()
    {
        int eventId;

        using (var seedScope = _serviceProvider.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var @event = await AddEventAsync(seedContext, totalSeats: 5);
            eventId = @event.Id;
        }

        var attempts = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

                try
                {
                    await service.CreateBookingAsync(eventId, UserId);
                    return "success";
                }
                catch (NoAvailableSeatsException)
                {
                    return "noSeats";
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(attempts);
        using var assertScope = _serviceProvider.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingService = assertScope.ServiceProvider.GetRequiredService<IBookingService>();
        var pendingBookings = await bookingService.GetPendingBookingsAsync();
        var persistedEvent = await assertContext.Events.FindAsync(eventId);

        results.Count(x => x == "success").Should().Be(5);
        results.Count(x => x == "noSeats").Should().Be(15);
        pendingBookings.Should().HaveCount(5);
        persistedEvent!.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldThrowValidationException_WhenStatusIsPending()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        Func<Task> act = () => service.UpdateBookingStatusAsync(999999, BookingStatus.Pending);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Status must be Confirmed or Rejected*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowValidationException_WhenEventIdIsInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        Func<Task> act = () => service.CreateBookingAsync(0, UserId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*EventId must be greater than 0*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowEventAlreadyStartedException_WhenEventAlreadyStarted()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var @event = new Event("Started event", null, startedAt, startedAt.AddHours(1), totalSeats: 10);
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        Func<Task> act = () => service.CreateBookingAsync(@event.Id, UserId);

        await act.Should().ThrowAsync<EventAlreadyStartedException>()
            .WithMessage("*already started*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowActiveBookingLimitExceededException_WhenUserReachedLimit()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context, totalSeats: 11);

        for (var i = 0; i < 10; i++)
        {
            await service.CreateBookingAsync(@event.Id, UserId);
        }

        Func<Task> act = () => service.CreateBookingAsync(@event.Id, UserId);

        await act.Should().ThrowAsync<ActiveBookingLimitExceededException>()
            .WithMessage("*10*");
        @event.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldApplyActiveBookingLimitPerUser()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context, totalSeats: 11);

        for (var i = 0; i < 10; i++)
        {
            await service.CreateBookingAsync(@event.Id, UserId);
        }

        var booking = await service.CreateBookingAsync(@event.Id, OtherUserId);

        booking.UserId.Should().Be(OtherUserId);
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldThrowForbiddenOperationException_WhenUserCancelsAnotherUsersBooking()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context);
        var booking = await service.CreateBookingAsync(@event.Id, UserId);

        Func<Task> act = () => service.CancelBookingAsync(booking.Id, OtherUserId, UserRole.User);

        await act.Should().ThrowAsync<ForbiddenOperationException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldCancelOwnBookingAndReleaseSeat()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var @event = await AddEventAsync(context);
        var booking = await service.CreateBookingAsync(@event.Id, UserId);
        var availableSeatsBeforeCancel = @event.AvailableSeats;

        var result = await service.CancelBookingAsync(booking.Id, UserId, UserRole.User);

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().NotBeNull();
        @event.AvailableSeats.Should().Be(availableSeatsBeforeCancel + 1);
    }

    [Fact]
    public void Confirm_ShouldSetConfirmedStatusAndProcessedAt()
    {
        var createdAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        var processedAt = createdAt.AddMinutes(5);
        var booking = new Booking(eventId: 1, userId: UserId, createdAt: createdAt);

        booking.Confirm(processedAt);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private static async Task<Event> AddEventAsync(AppDbContext context, int totalSeats = 10)
    {
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = new Event("Booking test event", null, startAt, startAt.AddHours(1), totalSeats);
        context.Events.Add(@event);
        await context.SaveChangesAsync();

        return @event;
    }
}

