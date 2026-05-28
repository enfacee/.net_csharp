public class BookingProcessingBackgroundService(
    IBookingStore bookingStore,
    IEventStore eventStore,
    ILogger<BookingProcessingBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Booking processing background service started.");

        using var timer = new PeriodicTimer(PollingInterval);

        try
        {
            do
            {
                await ProcessPendingBookingsAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Booking processing background service stopped.");
        }
    }

    private async Task ProcessPendingBookingsAsync(CancellationToken stoppingToken)
    {
        var pendingBookings = bookingStore.GetPending().ToList();
        var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));

        await Task.WhenAll(tasks);
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Processing booking {BookingId}.", booking.Id);

            await Task.Delay(ProcessingDelay, stoppingToken);

            await _processingSemaphore.WaitAsync(stoppingToken);
            try
            {
                if (eventStore.GetById(booking.EventId) is null)
                {
                    booking.Reject();
                    bookingStore.TryUpdate(booking);
                    logger.LogWarning(
                        "Booking {BookingId} rejected because event {EventId} was not found.",
                        booking.Id,
                        booking.EventId);
                    return;
                }

                booking.Confirm();
                bookingStore.TryUpdate(booking);
                logger.LogInformation(
                    "Booking {BookingId} processed with status {Status}.",
                    booking.Id,
                    booking.Status);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RejectBookingAndReleaseSeatAsync(booking, exception);
        }
    }

    private async Task RejectBookingAndReleaseSeatAsync(Booking booking, Exception exception)
    {
        logger.LogError(exception, "Failed to process booking {BookingId}.", booking.Id);

        await _processingSemaphore.WaitAsync();
        try
        {
            booking.Reject();
            bookingStore.TryUpdate(booking);

            if (eventStore.GetById(booking.EventId) is { } @event)
            {
                @event.ReleaseSeats();
                eventStore.TryUpdate(@event);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }
}
