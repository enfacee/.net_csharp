using System.ComponentModel.DataAnnotations;
using EventApi;
using EventApi.Application.Abstractions;
using EventApi.Application.Services;
using EventApi.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventApi.Tests;

public class EventServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public EventServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateEventAsync_ShouldCreateEventWithSeats()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var endAt = startAt.AddHours(1);

        var @event = await service.CreateEventAsync("Architecture review", null, startAt, endAt, totalSeats: 15);

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
        var from = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 05, 11, 23, 59, 59, DateTimeKind.Utc);

        await service.AddAsync(CreateEvent("Team sync", startAt: from, endAt: from.AddHours(1)));
        await service.AddAsync(CreateEvent("Client sync", startAt: from.AddDays(1), endAt: from.AddDays(1).AddHours(1)));
        await service.AddAsync(CreateEvent("Team retro", startAt: from.AddDays(2), endAt: from.AddDays(2).AddHours(1)));
        await service.AddAsync(CreateEvent("Workshop", startAt: from, endAt: from.AddHours(2)));

        var result = await service.GetAllAsync(title: "sync", from: from, to: to, page: 1, pageSize: 10);

        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Team sync", "Client sync");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var @event = CreateEvent("Sprint planning");
        await service.AddAsync(@event);

        @event.Title = "Updated sprint planning";
        @event.Description = "Updated description";
        @event.EndAt = @event.StartAt.AddHours(2);

        await service.UpdateAsync(@event);

        var result = await service.GetByIdAsync(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated sprint planning");
        result.Description.Should().Be("Updated description");
        result.EndAt.Should().Be(@event.StartAt.AddHours(2));
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteExistingEvent()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var @event = CreateEvent("To remove");
        await service.AddAsync(@event);

        var removed = await service.RemoveAsync(@event.Id);

        removed.Should().BeTrue();
        (await service.GetByIdAsync(@event.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ShouldThrowValidationException_WhenTitleIsInvalid()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();
        var startAt = new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var endAt = startAt.AddHours(1);

        Func<Task> act = () => service.AddAsync(new Event("   ", null, startAt, endAt));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Title is required*");
    }

    [Fact]
    public void Create_ShouldThrowArgumentOutOfRangeException_WhenTotalSeatsIsInvalid()
    {
        var startAt = new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var endAt = startAt.AddHours(1);

        Action act = () => Event.Create("Invalid capacity", null, startAt, endAt, totalSeats: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*TotalSeats must be greater than 0*");
    }

    [Fact]
    public void TryReserveSeats_ShouldDecreaseAvailableSeats_WhenEnoughSeatsExist()
    {
        var @event = CreateEvent("Limited event", totalSeats: 3);

        var reserved = @event.TryReserveSeats(2);

        reserved.Should().BeTrue();
        @event.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public void ReleaseSeats_ShouldIncreaseAvailableSeatsWithoutExceedingTotalSeats()
    {
        var @event = CreateEvent("Limited event", totalSeats: 3);
        @event.TryReserveSeats(2);

        @event.ReleaseSeats(5);

        @event.AvailableSeats.Should().Be(3);
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

    private static Event CreateEvent(
        string title,
        string? description = null,
        DateTime? startAt = null,
        DateTime? endAt = null,
        int totalSeats = 1)
    {
        var actualStartAt = startAt ?? new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var actualEndAt = endAt ?? actualStartAt.AddHours(1);

        return new Event(title, description, actualStartAt, actualEndAt, totalSeats);
    }
}

