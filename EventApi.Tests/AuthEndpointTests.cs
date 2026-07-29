using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventApi.Application.DTO;
using EventApi.Domain.Entities;
using FluentAssertions;

namespace EventApi.Tests;

public class AuthEndpointTests(EventApiWebApplicationFactory factory) : IClassFixture<EventApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task RegisterAndLogin_ShouldReturnJwtToken()
    {
        var client = factory.CreateClient();
        var login = UniqueLogin("user");

        var registerResponse = await RegisterAsync(client, login, UserRole.User);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = await LoginAsync(client, login);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AdminEndpoints_ShouldReturnUnauthorizedWithoutToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/events", CreateEventRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoints_ShouldReturnForbiddenForRegularUser()
    {
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, UniqueLogin("user"), UserRole.User);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/events", CreateEventRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoints_ShouldAllowAdminToCreateEvent()
    {
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, UniqueLogin("admin"), UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/events", CreateEventRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BookingEndpoints_ShouldRequireAuthentication()
    {
        var client = factory.CreateClient();

        var createBookingResponse = await client.PostAsync("/events/1/book", content: null);
        var getBookingResponse = await client.GetAsync("/bookings/1");
        var deleteBookingResponse = await client.DeleteAsync("/bookings/1");

        createBookingResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        getBookingResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        deleteBookingResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BookingFlow_ShouldAllowAuthenticatedUserToCreateGetAndCancelOwnBooking()
    {
        var client = factory.CreateClient();
        var adminToken = await RegisterAndLoginAsync(client, UniqueLogin("admin"), UserRole.Admin);
        var userToken = await RegisterAndLoginAsync(client, UniqueLogin("user"), UserRole.User);
        var eventId = await CreateEventAsync(client, adminToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var bookingResponse = await client.PostAsync($"/events/{eventId}/book", content: null);
        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var booking = await bookingResponse.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions);
        booking.Should().NotBeNull();

        var getResponse = await client.GetAsync($"/bookings/{booking!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync($"/bookings/{booking.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBooking_ShouldReturnForbidden_WhenUserCancelsAnotherUsersBooking()
    {
        var client = factory.CreateClient();
        var adminToken = await RegisterAndLoginAsync(client, UniqueLogin("admin"), UserRole.Admin);
        var ownerToken = await RegisterAndLoginAsync(client, UniqueLogin("owner"), UserRole.User);
        var otherUserToken = await RegisterAndLoginAsync(client, UniqueLogin("other"), UserRole.User);
        var eventId = await CreateEventAsync(client, adminToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var bookingResponse = await client.PostAsync($"/events/{eventId}/book", content: null);
        var booking = await bookingResponse.Content.ReadFromJsonAsync<BookingResponse>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherUserToken);
        var deleteResponse = await client.DeleteAsync($"/bookings/{booking!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string login, UserRole role)
    {
        var registerResponse = await RegisterAsync(client, login, role);
        registerResponse.EnsureSuccessStatusCode();

        return await LoginAsync(client, login);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string login, UserRole role)
    {
        return await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Login = login,
            Password = "password",
            Role = role
        });
    }

    private static async Task<string> LoginAsync(HttpClient client, string login)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Login = login,
            Password = "password"
        });
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResponse!.Token;
    }

    private static async Task<int> CreateEventAsync(HttpClient client, string adminToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("/events", CreateEventRequest());
        response.EnsureSuccessStatusCode();

        var @event = await response.Content.ReadFromJsonAsync<EventResponse>();
        return @event!.Id;
    }

    private static EventRequest CreateEventRequest()
    {
        var startAt = DateTime.UtcNow.AddDays(30);

        return new EventRequest
        {
            Title = "Authorization test event",
            Description = "Created from auth endpoint tests",
            StartAt = startAt,
            EndAt = startAt.AddHours(1),
            TotalSeats = 10
        };
    }

    private static string UniqueLogin(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }
}
