using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventsService.Tests;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ProcessPendingBookingsAsync_ShouldRejectBooking_WhenEventWasDeleted()
    {
        var bookingStore = new InMemoryBookingStore();
        var eventStore = new InMemoryEventStore();
        var booking = new Booking(eventId: 999999);
        bookingStore.TryAdd(booking);
        var service = CreateService(bookingStore, eventStore);

        await ProcessPendingBookingsAsync(service);

        var result = bookingStore.GetById(booking.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingBookingsAsync_ShouldRejectBookingAndReleaseSeat_WhenUnexpectedErrorOccurs()
    {
        var eventStore = new InMemoryEventStore();
        var bookingStore = new ThrowOnceOnUpdateBookingStore();
        var @event = CreateTestEvent(totalSeats: 1);
        @event.TryReserveSeats();
        eventStore.TryAdd(@event);
        var booking = new Booking(@event.Id);
        bookingStore.TryAdd(booking);
        var service = CreateService(bookingStore, eventStore);

        await ProcessPendingBookingsAsync(service);

        var result = bookingStore.GetById(booking.Id);
        result.Should().NotBeNull();
        result!.Status.Should().Be(BookingStatus.Rejected);
        result.ProcessedAt.Should().NotBeNull();
        @event.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPendingBookingsAsync_ShouldRunExternalDelayInParallel()
    {
        var bookingStore = new InMemoryBookingStore();
        var eventStore = new InMemoryEventStore();

        for (var i = 0; i < 3; i++)
        {
            var @event = CreateTestEvent(totalSeats: 1);
            eventStore.TryAdd(@event);
            bookingStore.TryAdd(new Booking(@event.Id));
        }

        var service = CreateService(bookingStore, eventStore);
        var stopwatch = Stopwatch.StartNew();

        await ProcessPendingBookingsAsync(service);

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
        bookingStore.GetAll().Should().OnlyContain(booking => booking.Status == BookingStatus.Confirmed);
    }

    private static BookingProcessingBackgroundService CreateService(
        IBookingStore bookingStore,
        IEventStore eventStore)
    {
        return new BookingProcessingBackgroundService(
            bookingStore,
            eventStore,
            NullLogger<BookingProcessingBackgroundService>.Instance);
    }

    private static async Task ProcessPendingBookingsAsync(BookingProcessingBackgroundService service)
    {
        var method = typeof(BookingProcessingBackgroundService).GetMethod(
            "ProcessPendingBookingsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var task = (Task)method!.Invoke(service, [CancellationToken.None])!;
        await task;
    }

    private static Event CreateTestEvent(int totalSeats)
    {
        var startAt = new DateTime(2026, 05, 22, 10, 0, 0, DateTimeKind.Utc);
        return new Event("Background test event", null, startAt, startAt.AddHours(1), totalSeats);
    }

    private sealed class ThrowOnceOnUpdateBookingStore : IBookingStore
    {
        private readonly InMemoryBookingStore _inner = new();
        private bool _shouldThrow = true;

        public bool TryAdd(Booking booking) => _inner.TryAdd(booking);

        public IReadOnlyCollection<Booking> GetAll() => _inner.GetAll();

        public IReadOnlyCollection<Booking> GetPending() => _inner.GetPending();

        public Booking? GetById(int id) => _inner.GetById(id);

        public bool TryUpdate(Booking booking)
        {
            if (_shouldThrow)
            {
                _shouldThrow = false;
                throw new InvalidOperationException("Simulated storage failure.");
            }

            return _inner.TryUpdate(booking);
        }
    }
}
