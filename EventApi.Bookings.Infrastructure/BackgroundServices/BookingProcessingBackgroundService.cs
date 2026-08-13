using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventApi.Bookings.Infrastructure.BackgroundServices;

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
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var booking = await bookingService.UpdateBookingStatusAsync(
                bookingId,
                BookingStatus.Confirmed,
                stoppingToken);
            if (booking is null)
                return;

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
            await RejectBookingAsync(bookingId, exception, stoppingToken);
        }
    }

    private async Task RejectBookingAsync(
        int bookingId,
        Exception exception,
        CancellationToken stoppingToken)
    {
        logger.LogError(exception, "Failed to process booking {BookingId}.", bookingId);

        using var scope = scopeFactory.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        await bookingService.UpdateBookingStatusAsync(
            bookingId,
            BookingStatus.Rejected,
            stoppingToken);
    }
}
