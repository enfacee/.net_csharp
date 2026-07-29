using System.IdentityModel.Tokens.Jwt;
using EventApi.Application.Abstractions;
using EventApi.Application.DTO;
using EventApi.Application.Mapping;
using EventApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Presentation.Controllers;

[ApiController]
[Route("[controller]")]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    public const string GetByIdRouteName = "GetBookingById";

    [HttpGet("{id}", Name = GetByIdRouteName)]
    [Authorize]
    public async Task<ActionResult<BookingResponse>> GetById(int id)
    {
        if (await bookingService.GetBookingByIdAsync(id) is not { } booking)
            return NotFound();

        return Ok(booking.ToResponse());
    }

    [HttpDelete("{id}")]
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

