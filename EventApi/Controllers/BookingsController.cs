using EventApi.Application.Abstractions;
using EventApi.Application.DTO;
using EventApi.Application.Mapping;
using EventApi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EventApi;

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

