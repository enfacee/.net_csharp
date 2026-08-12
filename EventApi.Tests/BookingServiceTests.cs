using System.ComponentModel.DataAnnotations;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Application.Services;
using EventApi.Bookings.Domain.Entities;
using EventApi.Bookings.Domain.Exceptions;
using EventApi.Bookings.Infrastructure.Persistence;
using EventApi.Bookings.Infrastructure.Persistence.Repositories;
using EventApi.Shared.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Tests;

public sealed class BookingServiceTests : IDisposable
{
    private const int EventId = 100;
    private const int UserId = 1;
    private const int OtherUserId = 2;

    private readonly ServiceProvider _serviceProvider;
    private readonly FakeBookingEventPublisher _publisher = new();

    public BookingServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<BookingsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBookingEventPublisher>(_publisher);
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreatePendingBookingAndPublishBookingCreated()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await service.CreateBookingAsync(EventId, UserId);

        booking.Id.Should().BeGreaterThan(0);
        booking.EventId.Should().Be(EventId);
        booking.UserId.Should().Be(UserId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
        _publisher.Created.Should().ContainSingle(message =>
            message.BookingId == booking.Id &&
            message.EventId == EventId &&
            message.UserId == UserId &&
            message.Seats == 1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowActiveBookingLimitExceededException_WhenUserReachedLimit()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var i = 0; i < 10; i++)
        {
            await service.CreateBookingAsync(EventId + i, UserId);
        }

        Func<Task> act = () => service.CreateBookingAsync(EventId + 10, UserId);

        await act.Should().ThrowAsync<ActiveBookingLimitExceededException>()
            .WithMessage("*10*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldApplyActiveBookingLimitPerUser()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var i = 0; i < 10; i++)
        {
            await service.CreateBookingAsync(EventId + i, UserId);
        }

        var booking = await service.CreateBookingAsync(EventId, OtherUserId);

        booking.UserId.Should().Be(OtherUserId);
    }

    [Fact]
    public async Task ConfirmBookingAsync_ShouldConfirmPendingBookingAndPublishBookingConfirmed()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await service.CreateBookingAsync(EventId, UserId);

        var result = await service.ConfirmBookingAsync(booking.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Confirmed);
        result.ProcessedAt.Should().NotBeNull();
        _publisher.Confirmed.Should().ContainSingle(message =>
            message.BookingId == booking.Id &&
            message.EventId == EventId &&
            message.UserId == UserId &&
            message.Seats == 1);
    }

    [Fact]
    public async Task ConfirmBookingAsync_ShouldBeIdempotent_WhenBookingAlreadyProcessed()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await service.CreateBookingAsync(EventId, UserId);
        await service.ConfirmBookingAsync(booking.Id);

        await service.ConfirmBookingAsync(booking.Id);

        _publisher.Confirmed.Should().ContainSingle();
    }

    [Fact]
    public async Task RejectBookingAsync_ShouldRejectPendingBookingAndPublishBookingRejected()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await service.CreateBookingAsync(EventId, UserId);

        var result = await service.RejectBookingAsync(booking.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
        _publisher.Rejected.Should().ContainSingle(message =>
            message.BookingId == booking.Id &&
            message.EventId == EventId &&
            message.UserId == UserId &&
            message.Seats == 1);
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookings()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var pendingBooking = await service.CreateBookingAsync(EventId, UserId);
        var confirmedBooking = await service.CreateBookingAsync(EventId + 1, UserId);
        await service.ConfirmBookingAsync(confirmedBooking.Id);

        var result = await service.GetPendingBookingsAsync();

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(pendingBooking.Id);
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldThrowForbiddenOperationException_WhenUserCancelsAnotherUsersBooking()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await service.CreateBookingAsync(EventId, UserId);

        Func<Task> act = () => service.CancelBookingAsync(booking.Id, OtherUserId, UserRole.User);

        await act.Should().ThrowAsync<ForbiddenOperationException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public async Task CancelBookingAsync_ShouldCancelOwnBooking()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var booking = await service.CreateBookingAsync(EventId, UserId);

        var result = await service.CancelBookingAsync(booking.Id, UserId, UserRole.User);

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowValidationException_WhenIdsAreInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        Func<Task> invalidEventId = () => service.CreateBookingAsync(0, UserId);
        Func<Task> invalidUserId = () => service.CreateBookingAsync(EventId, 0);

        await invalidEventId.Should().ThrowAsync<ValidationException>()
            .WithMessage("*EventId must be greater than 0*");
        await invalidUserId.Should().ThrowAsync<ValidationException>()
            .WithMessage("*UserId must be greater than 0*");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private sealed class FakeBookingEventPublisher : IBookingEventPublisher
    {
        public List<BookingCreated> Created { get; } = [];
        public List<BookingConfirmed> Confirmed { get; } = [];
        public List<BookingRejected> Rejected { get; } = [];

        public Task PublishBookingCreatedAsync(BookingCreated message, CancellationToken cancellationToken = default)
        {
            Created.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishBookingConfirmedAsync(BookingConfirmed message, CancellationToken cancellationToken = default)
        {
            Confirmed.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishBookingRejectedAsync(BookingRejected message, CancellationToken cancellationToken = default)
        {
            Rejected.Add(message);
            return Task.CompletedTask;
        }
    }
}
