using EventApi.Events.Application.Abstractions;
using EventApi.Events.Application.Common;
using EventApi.Events.Application.DTO;
using EventApi.Events.Application.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Events.Controllers;

[ApiController]
[Route("[controller]")]
public class EventsController(IEventService eventService) : ControllerBase
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
            Items = result.Items.Select(e => e.ToResponse()).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        if (await eventService.GetByIdAsync(id) is { } @event)
            return Ok(@event.ToResponse());

        return NotFound();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EventResponse>> Create([FromBody] EventRequest request)
    {
        var @event = await eventService.CreateEventAsync(
            request.Title!,
            request.Description,
            request.StartAt,
            request.EndAt,
            request.TotalSeats!.Value);

        var response = @event.ToResponse();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, [FromBody] EventRequest request)
    {
        if (!await eventService.UpdateEventAsync(id, request))
            return NotFound();

        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!await eventService.RemoveAsync(id))
            return NotFound();

        return Ok();
    }
}
