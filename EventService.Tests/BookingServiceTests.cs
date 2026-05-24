using System.ComponentModel.DataAnnotations;
using FluentAssertions;

namespace EventsService.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_ShouldCreatePendingBooking_WhenEventExists()
    {
        var (service, @event) = CreateServiceWithEvent();
        var beforeCreate = DateTime.UtcNow;

        var booking = await service.CreateBookingAsync(@event.Id);

        var afterCreate = DateTime.UtcNow;
        booking.Id.Should().BeGreaterThan(0);
        booking.EventId.Should().Be(@event.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        booking.CreatedAt.Should().BeOnOrBefore(afterCreate);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForMultipleBookingsOfSameEvent()
    {
        var (service, @event) = CreateServiceWithEvent();

        var first = await service.CreateBookingAsync(@event.Id);
        var second = await service.CreateBookingAsync(@event.Id);
        var third = await service.CreateBookingAsync(@event.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
        first.EventId.Should().Be(@event.Id);
        second.EventId.Should().Be(@event.Id);
        third.EventId.Should().Be(@event.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnBooking_WhenItExists()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);

        var result = await service.GetBookingByIdAsync(booking.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(booking.Id);
        result.EventId.Should().Be(@event.Id);
        result.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReflectConfirmedStatus()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);

        await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed);
        var result = await service.GetBookingByIdAsync(booking.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Confirmed);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReflectRejectedStatus()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);

        await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected);
        var result = await service.GetBookingByIdAsync(booking.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingBookingsAsync_ShouldReturnOnlyPendingBookings()
    {
        var (service, @event) = CreateServiceWithEvent();
        var pendingBooking = await service.CreateBookingAsync(@event.Id);
        var confirmedBooking = await service.CreateBookingAsync(@event.Id);
        await service.UpdateBookingStatusAsync(confirmedBooking.Id, BookingStatus.Confirmed);

        var result = await service.GetPendingBookingsAsync();

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(pendingBooking.Id);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldConfirmPendingBookingAndSetProcessedAt()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);
        var beforeConfirm = DateTime.UtcNow;

        var result = await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed);

        var afterConfirm = DateTime.UtcNow;
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Confirmed);
        result.ProcessedAt.Should().NotBeNull();
        result.ProcessedAt.Should().BeOnOrAfter(beforeConfirm);
        result.ProcessedAt.Should().BeOnOrBefore(afterConfirm);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldRejectPendingBookingAndSetProcessedAt()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);
        var beforeReject = DateTime.UtcNow;

        var result = await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected);

        var afterReject = DateTime.UtcNow;
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
        result.ProcessedAt.Should().BeOnOrAfter(beforeReject);
        result.ProcessedAt.Should().BeOnOrBefore(afterReject);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventDoesNotExist()
    {
        var service = CreateService(new EventService());

        Func<Task> act = () => service.CreateBookingAsync(999999);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Event not found*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowKeyNotFoundException_WhenEventWasDeleted()
    {
        var eventService = new EventService();
        var @event = CreateEvent();
        eventService.Add(@event);
        eventService.Remove(@event.Id);
        var service = CreateService(eventService);

        Func<Task> act = () => service.CreateBookingAsync(@event.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Event not found*");
    }

    [Fact]
    public async Task GetBookingByIdAsync_ShouldReturnNull_WhenBookingDoesNotExist()
    {
        var (service, _) = CreateServiceWithEvent();

        var result = await service.GetBookingByIdAsync(999999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowValidationException_WhenEventIdIsInvalid()
    {
        var service = CreateService(new EventService());

        Func<Task> act = () => service.CreateBookingAsync(0);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*EventId is required*");
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldReturnNull_WhenBookingDoesNotExist()
    {
        var (service, _) = CreateServiceWithEvent();

        var result = await service.UpdateBookingStatusAsync(999999, BookingStatus.Confirmed);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldThrowValidationException_WhenStatusIsPending()
    {
        var (service, _) = CreateServiceWithEvent();

        Func<Task> act = () => service.UpdateBookingStatusAsync(999999, BookingStatus.Pending);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Status must be Confirmed or Rejected*");
    }

    private static (BookingService Service, Event Event) CreateServiceWithEvent()
    {
        var eventService = new EventService();
        var @event = CreateEvent();
        eventService.Add(@event);

        return (CreateService(eventService), @event);
    }

    private static BookingService CreateService(IEventService eventService) => new(eventService);

    private static Event CreateEvent()
    {
        var startAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        return new Event("Booking test event", null, startAt, startAt.AddHours(1));
    }
}
