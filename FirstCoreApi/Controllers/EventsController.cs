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
    public ActionResult<EventResponse[]> GetAll()
    {
        return Ok(_eventService.GetAll().Select(e => e.CreateFrom<Event, EventResponse>()).ToArray());
    }
    [HttpGet("{id}")]
    public ActionResult<EventResponse> GetById(int id)
    {
        if (_eventService.GetById(id) is {} evnt)
            return Ok(evnt.CreateFrom<Event, EventResponse>());
        return NotFound();
    }
    [HttpPost]
    public ActionResult Create([FromBody] EventRequest request)
    {
        var entity = new Event
        {
            Id = _eventService.GenerateId(),
            Title = request.Title!,
            Description = request.Description,
            StartAt = request.StartAt,
            EndAt = request.EndAt
        };
        _eventService.Add(entity);
        var response = entity.CreateFrom<Event, EventResponse>();
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    public ActionResult Update(int id, [FromBody] EventRequest request)
    {
        if (_eventService.GetById(id) is not {} entity)
            return NotFound();
        entity.CopyFrom(request);
        _eventService.Update(entity);
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