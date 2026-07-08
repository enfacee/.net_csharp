using Microsoft.AspNetCore.Mvc;

namespace EventApi;

[ApiController]
[Route("[controller]")]
public class EventsController(IEventService eventService, IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<EventResponse>>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await eventService.GetAllAsync(title, from, to, page, pageSize);

        return Ok(new PaginatedResult<EventResponse>
        {
            TotalCount = result.TotalCount,
            Items = result.Items.Select(e => e.CreateFrom<Event, EventResponse>()).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize
        });
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        if (await eventService.GetByIdAsync(id) is {} @event)
            return Ok(@event.CreateFrom<Event, EventResponse>());
        return NotFound();
    }
    [HttpPost]
    public async Task<ActionResult<EventResponse>> Create([FromBody] EventRequest request)
    {
        var @event = await eventService.CreateEventAsync(
            request.Title!,
            request.Description,
            request.StartAt,
            request.EndAt,
            request.TotalSeats!.Value);
        var response = @event.CreateFrom<Event, EventResponse>();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPost("{id}/book")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(int id)
    {
        var booking = await bookingService.CreateBookingAsync(id);
        var response = booking.CreateFrom<Booking, BookingResponse>();

        return AcceptedAtRoute(
            BookingsController.GetByIdRouteName,
            new { id = response.Id },
            response);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] EventRequest request)
    {
        if (await eventService.GetByIdAsync(id) is not {} @event)
            return NotFound();
        @event.CopyFrom(request);
        await eventService.UpdateAsync(@event);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!await eventService.RemoveAsync(id))
            return NotFound();
        return Ok();
    }
}

