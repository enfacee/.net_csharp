using EventApi.Users.Application.Abstractions;
using EventApi.Users.Application.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventApi.Users.Controllers;

[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class AuthController(IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await userService.RegisterAsync(
            request.Login!,
            request.Password!,
            request.Role,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await userService.LoginAsync(
            request.Login!,
            request.Password!,
            cancellationToken);

        if (token is null)
            return NotFound("Invalid login or password.");

        return Ok(new LoginResponse { Token = token });
    }
}
