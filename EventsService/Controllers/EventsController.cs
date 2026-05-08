using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }
    [HttpGet]
    public ActionResult<PaginatedResult<EventResponse>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = _eventService.GetAll(title, from, to, page, pageSize);

        return Ok(new PaginatedResult<EventResponse>
        {
            TotalCount = result.TotalCount,
            Items = result.Items.Select(e => e.CreateFrom<Event, EventResponse>()).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize
        });
    }
    [HttpGet("{id}")]
    public ActionResult<EventResponse> GetById(int id)
    {
        if (_eventService.GetById(id) is {} @event)
            return Ok(@event.CreateFrom<Event, EventResponse>());
        return NotFound();
    }
    [HttpPost]
    public ActionResult Create([FromBody] EventRequest request)
    {
        var @event = new Event(request.Title!, request.Description, request.StartAt, request.EndAt);
        _eventService.Add(@event);
        var response = @event.CreateFrom<Event, EventResponse>();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    public ActionResult Update(int id, [FromBody] EventRequest request)
    {
        if (_eventService.GetById(id) is not {} @event)
            return NotFound();
        @event.CopyFrom(request);
        _eventService.Update(@event);
        return Ok();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        if (!_eventService.Remove(id))
            return NotFound();
        return Ok();
    }
}
