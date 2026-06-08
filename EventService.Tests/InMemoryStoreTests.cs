using FluentAssertions;

namespace EventsService.Tests;

public class InMemoryStoreTests
{
    [Fact]
    public void EventStore_ShouldAddAndReturnEventById()
    {
        var store = new InMemoryEventStore();
        var @event = CreateTestEvent("Store event");

        var added = store.TryAdd(@event);
        var result = store.GetById(@event.Id);

        added.Should().BeTrue();
        result.Should().BeSameAs(@event);
    }

    [Fact]
    public void EventStore_ShouldReturnAllEvents()
    {
        var store = new InMemoryEventStore();
        var first = CreateTestEvent("First");
        var second = CreateTestEvent("Second");
        store.TryAdd(first);
        store.TryAdd(second);

        var result = store.GetAll();

        result.Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public void EventStore_ShouldRemoveExistingEvent()
    {
        var store = new InMemoryEventStore();
        var @event = CreateTestEvent("To remove");
        store.TryAdd(@event);

        var removed = store.TryRemove(@event.Id);

        removed.Should().BeTrue();
        store.GetById(@event.Id).Should().BeNull();
    }

    [Fact]
    public void EventStore_ShouldReturnFalse_WhenRemovingMissingEvent()
    {
        var store = new InMemoryEventStore();

        var removed = store.TryRemove(999999);

        removed.Should().BeFalse();
    }

    [Fact]
    public void EventStore_ShouldUpdateExistingEvent()
    {
        var store = new InMemoryEventStore();
        var original = CreateTestEvent("Original");
        var updated = CreateTestEvent("Updated", totalSeats: original.TotalSeats);
        SetEventId(updated, original.Id);
        store.TryAdd(original);

        var updateResult = store.TryUpdate(updated);
        var result = store.GetById(original.Id);

        updateResult.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated");
    }

    [Fact]
    public void EventStore_ShouldReturnFalse_WhenUpdatingMissingEvent()
    {
        var store = new InMemoryEventStore();
        var @event = CreateTestEvent("Missing");

        var updateResult = store.TryUpdate(@event);

        updateResult.Should().BeFalse();
    }

    [Fact]
    public void BookingStore_ShouldAddAndReturnBookingById()
    {
        var store = new InMemoryBookingStore();
        var booking = new Booking(eventId: 1);

        var added = store.TryAdd(booking);
        var result = store.GetById(booking.Id);

        added.Should().BeTrue();
        result.Should().BeSameAs(booking);
    }

    [Fact]
    public void BookingStore_ShouldReturnAllBookings()
    {
        var store = new InMemoryBookingStore();
        var first = new Booking(eventId: 1);
        var second = new Booking(eventId: 2);
        store.TryAdd(first);
        store.TryAdd(second);

        var result = store.GetAll();

        result.Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public void BookingStore_ShouldReturnOnlyPendingBookings()
    {
        var store = new InMemoryBookingStore();
        var pending = new Booking(eventId: 1);
        var confirmed = new Booking(eventId: 2);
        confirmed.Confirm();
        var rejected = new Booking(eventId: 3);
        rejected.Reject();
        store.TryAdd(pending);
        store.TryAdd(confirmed);
        store.TryAdd(rejected);

        var result = store.GetPending();

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(pending.Id);
    }

    [Fact]
    public void BookingStore_ShouldUpdateExistingBooking()
    {
        var store = new InMemoryBookingStore();
        var booking = new Booking(eventId: 1);
        store.TryAdd(booking);
        booking.Confirm();

        var updateResult = store.TryUpdate(booking);
        var result = store.GetById(booking.Id);

        updateResult.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Confirmed);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void BookingStore_ShouldReturnFalse_WhenUpdatingMissingBooking()
    {
        var store = new InMemoryBookingStore();
        var booking = new Booking(eventId: 1);

        var updateResult = store.TryUpdate(booking);

        updateResult.Should().BeFalse();
    }

    private static Event CreateTestEvent(string title, int totalSeats = 10)
    {
        var startAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        return new Event(title, null, startAt, startAt.AddHours(1), totalSeats);
    }

    private static void SetEventId(Event @event, int id)
    {
        typeof(Event).GetProperty(nameof(Event.Id))!.SetValue(@event, id);
    }
}
