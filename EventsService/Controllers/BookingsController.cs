using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class BookingsController : ControllerBase
{
    public const string GetByIdRouteName = "GetBookingById";

    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet("{id}", Name = GetByIdRouteName)]
    public async Task<ActionResult<BookingResponse>> GetById(int id)
    {
        if (await _bookingService.GetBookingByIdAsync(id) is not { } booking)
            return NotFound();

        return Ok(booking.CreateFrom<Booking, BookingResponse>());
    }
}
