using System.ComponentModel.DataAnnotations;
using FluentAssertions;

namespace EventsService.Tests;

public class EventServiceTests
{
    [Fact]
    public void Add_ShouldCreateEvent()
    {
        var service = CreateService();
        var @event = CreateEvent("Architecture review");

        service.Add(@event);

        var result = service.GetById(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Architecture review");
    }

    [Fact]
    public void GetAll_ShouldReturnAllEvents()
    {
        var service = CreateServiceWithEvents(
            CreateEvent("Event 1"),
            CreateEvent("Event 2"),
            CreateEvent("Event 3"));

        var result = service.GetAll();

        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Event 1", "Event 2", "Event 3");
    }

    [Fact]
    public void GetById_ShouldReturnEvent_WhenItExists()
    {
        var @event = CreateEvent("Town hall");
        var service = CreateServiceWithEvents(@event);

        var result = service.GetById(@event.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(@event.Id);
        result.Title.Should().Be("Town hall");
    }

    [Fact]
    public void Update_ShouldUpdateExistingEvent()
    {
        var @event = CreateEvent("Sprint planning", startAt: DateTime.UtcNow, endAt: DateTime.UtcNow.AddHours(1));
        var service = CreateServiceWithEvents(@event);

        @event.Title = "Updated sprint planning";
        @event.Description = "Updated description";
        @event.EndAt = @event.StartAt.AddHours(2);

        service.Update(@event);

        var result = service.GetById(@event.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated sprint planning");
        result.Description.Should().Be("Updated description");
        result.EndAt.Should().Be(@event.StartAt.AddHours(2));
    }

    [Fact]
    public void Remove_ShouldDeleteExistingEvent()
    {
        var @event = CreateEvent("To remove");
        var service = CreateServiceWithEvents(@event);

        var removed = service.Remove(@event.Id);

        removed.Should().BeTrue();
        service.GetById(@event.Id).Should().BeNull();
    }

    [Fact]
    public void GetAll_ShouldFilterByTitle_CaseInsensitiveAndPartial()
    {
        var service = CreateServiceWithEvents(
            CreateEvent("Backend Meetup"),
            CreateEvent("Frontend demo"),
            CreateEvent("Team meeting"));

        var result = service.GetAll(title: "MEET");

        result.TotalCount.Should().Be(2);
        result.Items.Select(x => x.Title).Should().BeEquivalentTo(["Backend Meetup", "Team meeting"]);
    }

    [Fact]
    public void GetAll_ShouldFilterByDateRange_Inclusively()
    {
        var from = new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 05, 12, 11, 0, 0, DateTimeKind.Utc);
        var service = CreateServiceWithEvents(
            CreateEvent("Too early", startAt: from.AddDays(-1), endAt: from.AddHours(-1)),
            CreateEvent("On boundary start", startAt: from, endAt: from.AddHours(1)),
            CreateEvent("Inside range", startAt: from.AddDays(1), endAt: to.AddHours(-1)),
            CreateEvent("On boundary end", startAt: to.AddHours(-2), endAt: to),
            CreateEvent("Too late", startAt: to, endAt: to.AddHours(2)));

        var result = service.GetAll(from: from, to: to);

        result.TotalCount.Should().Be(3);
        result.Items.Select(x => x.Title).Should().ContainInOrder("On boundary start", "Inside range", "On boundary end");
    }

    [Fact]
    public void GetAll_ShouldPaginateEvents()
    {
        var service = CreateServiceWithEvents(
            CreateEvent("Event 1"),
            CreateEvent("Event 2"),
            CreateEvent("Event 3"),
            CreateEvent("Event 4"),
            CreateEvent("Event 5"));

        var result = service.GetAll(page: 2, pageSize: 2);

        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Event 3", "Event 4");
    }

    [Fact]
    public void GetAll_ShouldApplyCombinedFiltering()
    {
        var from = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 05, 11, 23, 59, 59, DateTimeKind.Utc);
        var service = CreateServiceWithEvents(
            CreateEvent("Team sync", startAt: from, endAt: from.AddHours(1)),
            CreateEvent("Client sync", startAt: from.AddDays(1), endAt: from.AddDays(1).AddHours(1)),
            CreateEvent("Team retro", startAt: from.AddDays(2), endAt: from.AddDays(2).AddHours(1)),
            CreateEvent("Workshop", startAt: from, endAt: from.AddHours(2)));

        var result = service.GetAll(title: "sync", from: from, to: to, page: 1, pageSize: 10);

        result.TotalCount.Should().Be(2);
        result.Items.Select(x => x.Title).Should().ContainInOrder("Team sync", "Client sync");
    }

    [Fact]
    public void GetById_ShouldReturnNull_WhenIdDoesNotExist()
    {
        var service = CreateService();

        var result = service.GetById(999999);

        result.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldNotChangeAnything_WhenEventDoesNotExist()
    {
        var existing = CreateEvent("Existing");
        var missing = CreateEvent("Missing");
        var service = CreateServiceWithEvents(existing);

        service.Update(missing);

        var result = service.GetAll();

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(existing.Id);
        result.Items[0].Title.Should().Be("Existing");
    }

    [Fact]
    public void Add_ShouldThrowValidationException_WhenTitleIsInvalid()
    {
        var service = CreateService();
        var startAt = new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var endAt = startAt.AddHours(1);

        Action act = () => service.Add(new Event("   ", null, startAt, endAt));

        act.Should().Throw<ValidationException>()
            .WithMessage("*Title is required*");
    }

    [Fact]
    public void Update_ShouldThrowValidationException_WhenDatesAreInvalid()
    {
        var startAt = new DateTime(2026, 05, 10, 10, 0, 0, DateTimeKind.Utc);
        var existing = CreateEvent("Persisted event", startAt: startAt, endAt: startAt.AddHours(1));
        var invalidUpdate = CreateEvent("Persisted event", startAt: startAt, endAt: startAt.AddMinutes(-1));
        var service = CreateServiceWithEvents(existing);

        SetEventId(invalidUpdate, existing.Id);

        Action act = () => service.Update(invalidUpdate);

        act.Should().Throw<ValidationException>()
            .WithMessage("*EndAt must be greater than StartAt*");

        var result = service.GetById(existing.Id);
        result.Should().NotBeNull();
        result!.EndAt.Should().Be(startAt.AddHours(1));
    }

    [Fact]
    public void GetAll_ShouldIgnoreEmptyOrWhitespaceTitleFilter()
    {
        var service = CreateServiceWithEvents(
            CreateEvent("Event 1"),
            CreateEvent("Event 2"));

        var emptyResult = service.GetAll(title: string.Empty);
        var whitespaceResult = service.GetAll(title: "   ");

        emptyResult.TotalCount.Should().Be(2);
        whitespaceResult.TotalCount.Should().Be(2);
    }

    [Fact]
    public void GetAll_ShouldReturnEmptyItems_WhenPageIsBeyondAvailableRange()
    {
        var service = CreateServiceWithEvents(
            CreateEvent("Event 1"),
            CreateEvent("Event 2"));

        var result = service.GetAll(page: 3, pageSize: 2);

        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(3);
        result.PageSize.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_ShouldThrowArgumentOutOfRangeException_WhenPageOrPageSizeIsInvalid()
    {
        var service = CreateServiceWithEvents(CreateEvent("Event 1"));

        Action invalidPage = () => service.GetAll(page: 0);
        Action invalidPageSize = () => service.GetAll(pageSize: 0);

        invalidPage.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Page must be greater than 0*");
        invalidPageSize.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*PageSize must be greater than 0*");
    }

    private static EventService CreateService() => new();

    private static EventService CreateServiceWithEvents(params Event[] events)
    {
        var service = CreateService();

        foreach (var @event in events)
        {
            service.Add(@event);
        }

        return service;
    }

    private static Event CreateEvent(
        string title,
        string? description = null,
        DateTime? startAt = null,
        DateTime? endAt = null)
    {
        var actualStartAt = startAt ?? new DateTime(2026, 05, 10, 9, 0, 0, DateTimeKind.Utc);
        var actualEndAt = endAt ?? actualStartAt.AddHours(1);

        return new Event(title, description, actualStartAt, actualEndAt);
    }

    private static void SetEventId(Event @event, int id)
    {
        typeof(Event).GetProperty(nameof(Event.Id))!.SetValue(@event, id);
    }
}
