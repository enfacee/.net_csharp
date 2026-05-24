public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IBookingService bookingService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking processing background service started.");

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
            _logger.LogInformation("Booking processing background service stopped.");
        }
    }

    private async Task ProcessPendingBookingsAsync(CancellationToken stoppingToken)
    {
        var pendingBookings = await _bookingService.GetPendingBookingsAsync(stoppingToken);

        foreach (var booking in pendingBookings)
        {
            try
            {
                _logger.LogInformation("Processing booking {BookingId}.", booking.Id);

                await Task.Delay(ProcessingDelay, stoppingToken);
                var processedBooking = await _bookingService.UpdateBookingStatusAsync(
                    booking.Id,
                    BookingStatus.Confirmed,
                    stoppingToken);

                if (processedBooking is not null)
                {
                    _logger.LogInformation(
                        "Booking {BookingId} processed with status {Status}.",
                        processedBooking.Id,
                        processedBooking.Status);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process booking {BookingId}.", booking.Id);
            }
        }
    }
}
