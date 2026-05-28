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
        @event.AvailableSeats.Should().Be(@event.TotalSeats - 1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_ForMultipleBookingsOfSameEvent()
    {
        var (service, @event) = CreateServiceWithEvent(totalSeats: 3);

        var first = await service.CreateBookingAsync(@event.Id);
        var second = await service.CreateBookingAsync(@event.Id);
        var third = await service.CreateBookingAsync(@event.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
        first.EventId.Should().Be(@event.Id);
        second.EventId.Should().Be(@event.Id);
        third.EventId.Should().Be(@event.Id);
        @event.AvailableSeats.Should().Be(0);
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
    public async Task UpdateBookingStatusAsync_ShouldRejectPendingBookingSetProcessedAtAndReleaseSeat()
    {
        var (service, @event) = CreateServiceWithEvent();
        var booking = await service.CreateBookingAsync(@event.Id);
        var beforeReject = DateTime.UtcNow;
        var availableSeatsBeforeReject = @event.AvailableSeats;

        var result = await service.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected);

        var afterReject = DateTime.UtcNow;
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
        result.ProcessedAt.Should().BeOnOrAfter(beforeReject);
        result.ProcessedAt.Should().BeOnOrBefore(afterReject);
        @event.AvailableSeats.Should().Be(availableSeatsBeforeReject + 1);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = CreateService(new EventService());

        Func<Task> act = () => service.CreateBookingAsync(999999);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event not found*");
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldThrowNotFoundException_WhenEventWasDeleted()
    {
        var eventService = new EventService();
        var @event = CreateTestEvent();
        eventService.Add(@event);
        eventService.Remove(@event.Id);
        var service = CreateService(eventService);

        Func<Task> act = () => service.CreateBookingAsync(@event.Id);

        await act.Should().ThrowAsync<NotFoundException>()
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
    public async Task CreateBookingAsync_ShouldThrowNoAvailableSeatsException_WhenEventHasNoAvailableSeats()
    {
        var (service, @event) = CreateServiceWithEvent(totalSeats: 1);
        await service.CreateBookingAsync(@event.Id);

        Func<Task> act = () => service.CreateBookingAsync(@event.Id);

        await act.Should().ThrowAsync<NoAvailableSeatsException>()
            .WithMessage("No available seats for this event");
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public void Confirm_ShouldSetConfirmedStatusAndProcessedAt()
    {
        var booking = new Booking(eventId: 1);
        var beforeConfirm = DateTime.UtcNow;

        booking.Confirm();

        var afterConfirm = DateTime.UtcNow;
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Should().BeOnOrAfter(beforeConfirm);
        booking.ProcessedAt.Should().BeOnOrBefore(afterConfirm);
    }

    [Fact]
    public void Reject_ShouldSetRejectedStatusAndProcessedAt()
    {
        var booking = new Booking(eventId: 1);
        var beforeReject = DateTime.UtcNow;

        booking.Reject();

        var afterReject = DateTime.UtcNow;
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt.Should().BeOnOrAfter(beforeReject);
        booking.ProcessedAt.Should().BeOnOrBefore(afterReject);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ShouldAllowNewBookingAfterRejectReleasesSeat()
    {
        var (service, @event) = CreateServiceWithEvent(totalSeats: 1);
        var rejectedBooking = await service.CreateBookingAsync(@event.Id);

        await service.UpdateBookingStatusAsync(rejectedBooking.Id, BookingStatus.Rejected);
        var newBooking = await service.CreateBookingAsync(@event.Id);

        newBooking.Id.Should().NotBe(rejectedBooking.Id);
        newBooking.EventId.Should().Be(@event.Id);
        newBooking.Status.Should().Be(BookingStatus.Pending);
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldAllowOnlyAvailableSeats_WhenCalledConcurrently()
    {
        var (service, @event) = CreateServiceWithEvent(totalSeats: 5);

        var attempts = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await service.CreateBookingAsync(@event.Id);
                    return "success";
                }
                catch (NoAvailableSeatsException)
                {
                    return "noSeats";
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(attempts);
        var pendingBookings = await service.GetPendingBookingsAsync();

        results.Count(x => x == "success").Should().Be(5);
        results.Count(x => x == "noSeats").Should().Be(15);
        pendingBookings.Should().HaveCount(5);
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_ShouldCreateUniqueIds_WhenCalledConcurrently()
    {
        var (service, @event) = CreateServiceWithEvent(totalSeats: 10);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => service.CreateBookingAsync(@event.Id)))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        bookings.Should().HaveCount(10);
        bookings.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        @event.AvailableSeats.Should().Be(0);
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

    private static (BookingService Service, Event Event) CreateServiceWithEvent(int totalSeats = 10)
    {
        var eventService = new EventService();
        var @event = CreateTestEvent(totalSeats);
        eventService.Add(@event);

        return (CreateService(eventService), @event);
    }

    private static BookingService CreateService(IEventService eventService) => new(eventService);

    private static Event CreateTestEvent(int totalSeats = 10)
    {
        var startAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        return new Event("Booking test event", null, startAt, startAt.AddHours(1), totalSeats);
    }
}
