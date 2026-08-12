using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.DTO;
using EventApi.Events.Application.Services;
using EventApi.Events.Domain.Entities;
using EventApi.Events.Infrastructure.Persistence;
using EventApi.Events.Infrastructure.Persistence.Repositories;
using EventApi.Shared.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly FakeEventSeatReservationPublisher _publisher = new();

    public EventServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<EventsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventSeatReservationPublisher>(_publisher);
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventSeatReservationService, EventSeatReservationService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateEventAsync_ShouldCreateEventWithAvailableSeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);

        var @event = await service.CreateEventAsync(
            "Architecture review",
            null,
            startAt,
            startAt.AddHours(1),
            totalSeats: 15);

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Architecture review");
        result.TotalSeats.Should().Be(15);
        result.AvailableSeats.Should().Be(15);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterAndPaginateEvents()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var from = DateTime.UtcNow.AddDays(10);
        var to = from.AddDays(2);

        await service.CreateEventAsync("Team sync", null, from, from.AddHours(1), 10);
        await service.CreateEventAsync("Client sync", null, from.AddDays(1), from.AddDays(1).AddHours(1), 10);
        await service.CreateEventAsync("Team retro", null, from.AddDays(3), from.AddDays(3).AddHours(1), 10);
        await service.CreateEventAsync("Workshop", null, from, from.AddHours(2), 10);

        var result = await service.GetAllAsync(title: "sync", from: from, to: to, page: 1, pageSize: 10);

        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Team sync", "Client sync");
    }

    [Fact]
    public async Task UpdateEventAsync_ShouldKeepReservedSeats_WhenTotalSeatsChanges()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await service.CreateEventAsync("Sprint planning", null, startAt, startAt.AddHours(1), 5);
        @event.TryReserveSeats(2);

        var updated = await service.UpdateEventAsync(
            @event.Id,
            new EventRequest
            {
                Title = "Updated sprint planning",
                Description = "Updated description",
                StartAt = startAt,
                EndAt = startAt.AddHours(2),
                TotalSeats = 7
            });

        var result = await service.GetByIdAsync(@event.Id);

        updated.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated sprint planning");
        result.TotalSeats.Should().Be(7);
        result.AvailableSeats.Should().Be(5);
    }

    [Fact]
    public async Task HandleBookingCreatedAsync_ShouldReserveSeatAndPublishReservedEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var reservationService = scope.ServiceProvider.GetRequiredService<IEventSeatReservationService>();
        var startAt = DateTime.UtcNow.AddDays(30);
        var @event = await eventService.CreateEventAsync("Kafka event", null, startAt, startAt.AddHours(1), 2);

        await reservationService.HandleBookingCreatedAsync(new BookingCreated(1, @event.Id, 10, 1, DateTime.UtcNow));

        var result = await eventService.GetByIdAsync(@event.Id);
        result!.AvailableSeats.Should().Be(1);
        _publisher.Reserved.Should().ContainSingle(message =>
            message.BookingId == 1 &&
            message.EventId == @event.Id &&
            message.UserId == 10 &&
            message.Seats == 1);
        _publisher.Unavailable.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleBookingCreatedAsync_ShouldPublishUnavailable_WhenEventAlreadyStarted()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var reservationService = scope.ServiceProvider.GetRequiredService<IEventSeatReservationService>();
        var startAt = DateTime.UtcNow.AddHours(-2);
        var @event = new Event("Started event", null, startAt, startAt.AddHours(1), 2);
        await eventRepository.AddAsync(@event);

        await reservationService.HandleBookingCreatedAsync(new BookingCreated(1, @event.Id, 10, 1, DateTime.UtcNow));

        _publisher.Reserved.Should().BeEmpty();
        _publisher.Unavailable.Should().ContainSingle(message =>
            message.BookingId == 1 &&
            message.EventId == @event.Id &&
            message.Reason.Contains("already started"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldThrowArgumentOutOfRangeException_WhenPageOrPageSizeIsInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        Func<Task> invalidPage = () => service.GetAllAsync(page: 0);
        Func<Task> invalidPageSize = () => service.GetAllAsync(pageSize: 0);

        await invalidPage.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*Page must be greater than 0*");
        await invalidPageSize.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*PageSize must be greater than 0*");
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private sealed class FakeEventSeatReservationPublisher : IEventSeatReservationPublisher
    {
        public List<EventSeatReserved> Reserved { get; } = [];
        public List<EventSeatUnavailable> Unavailable { get; } = [];

        public Task PublishSeatReservedAsync(EventSeatReserved message, CancellationToken cancellationToken = default)
        {
            Reserved.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishSeatUnavailableAsync(EventSeatUnavailable message, CancellationToken cancellationToken = default)
        {
            Unavailable.Add(message);
            return Task.CompletedTask;
        }
    }
}
