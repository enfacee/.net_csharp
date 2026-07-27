using EventApi.Application.Abstractions;
using EventApi.Domain.Entities;

namespace EventApi;

public class BookingProcessingBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingProcessingBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

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
        int[] pendingBookingIds;

        using (var scope = scopeFactory.CreateScope())
        {
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            pendingBookingIds = await bookingRepository.GetPendingBookingIdsAsync(stoppingToken);
        }

        var tasks = pendingBookingIds.Select(bookingId => ProcessBookingAsync(bookingId, stoppingToken));

        await Task.WhenAll(tasks);
    }

    private async Task ProcessBookingAsync(int bookingId, CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Processing booking {BookingId}.", bookingId);

            await Task.Delay(ProcessingDelay, stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
                return;

            if (!await eventRepository.ExistsAsync(booking.EventId, stoppingToken))
            {
                booking.Reject();
                await bookingRepository.SaveChangesAsync(stoppingToken);
                logger.LogWarning(
                    "Booking {BookingId} rejected because event {EventId} was not found.",
                    booking.Id,
                    booking.EventId);
                return;
            }

            booking.Confirm();
            await bookingRepository.SaveChangesAsync(stoppingToken);
            logger.LogInformation(
                "Booking {BookingId} processed with status {Status}.",
                booking.Id,
                booking.Status);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RejectBookingAndReleaseSeatAsync(bookingId, exception, stoppingToken);
        }
    }

    private async Task RejectBookingAndReleaseSeatAsync(
        int bookingId,
        Exception exception,
        CancellationToken stoppingToken)
    {
        logger.LogError(exception, "Failed to process booking {BookingId}.", bookingId);

        using var scope = scopeFactory.CreateScope();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
        if (booking is null)
            return;

        booking.Reject();

        if (await eventRepository.GetByIdAsync(booking.EventId, stoppingToken) is { } @event)
            @event.ReleaseSeats();

        await bookingRepository.SaveChangesAsync(stoppingToken);
    }
}

