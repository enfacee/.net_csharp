using System.IdentityModel.Tokens.Jwt;
using EventApi.Bookings.Application.Abstractions;
using EventApi.Bookings.Application.DTO;
using EventApi.Bookings.Application.Mapping;
using EventApi.Bookings.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Bookings.Controllers;

[ApiController]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    public const string GetByIdRouteName = "GetBookingById";

    [HttpPost("events/{eventId}/book")]
    [Authorize]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(
        int eventId,
        CancellationToken cancellationToken)
    {
        var booking = await bookingService.CreateBookingAsync(eventId, GetCurrentUserId(), cancellationToken);
        var response = booking.ToResponse();

        return AcceptedAtRoute(GetByIdRouteName, new { id = response.Id }, response);
    }

    [HttpGet("bookings/{id}", Name = GetByIdRouteName)]
    [Authorize]
    public async Task<ActionResult<BookingResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        if (await bookingService.GetBookingByIdAsync(id, cancellationToken) is not { } booking)
            return NotFound();

        return Ok(booking.ToResponse());
    }

    [HttpDelete("bookings/{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var currentUserRole = User.IsInRole(UserRole.Admin.ToString())
            ? UserRole.Admin
            : UserRole.User;

        if (!await bookingService.CancelBookingAsync(id, GetCurrentUserId(), currentUserRole, cancellationToken))
            return NotFound();

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userId, out var parsedUserId))
            throw new UnauthorizedAccessException("User id claim is missing or invalid.");

        return parsedUserId;
    }
}
