using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    public const string GetByIdRouteName = "GetBookingById";

    [HttpGet("{id}", Name = GetByIdRouteName)]
    public async Task<ActionResult<BookingResponse>> GetById(int id)
    {
        if (await bookingService.GetBookingByIdAsync(id) is not { } booking)
            return NotFound();

        return Ok(booking.CreateFrom<Booking, BookingResponse>());
    }
}
