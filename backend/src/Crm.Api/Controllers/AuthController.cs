using Crm.Api.Authentication;
using Crm.Api.Contracts.Auth;
using Crm.Api.Contracts.Profile;
using Crm.Application.Auth.Login;
using Crm.Application.Auth.Logout;
using Crm.Application.Auth.RegisterCustomer;
using Crm.Application.Auth.RenewSession;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
[AllowAnonymous]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [EndpointDescription("Anonymous. Creates a Customer account; the role is always assigned by the server.")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CurrentUserResponse>> Register(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var user = await sender.Send(
            new RegisterCustomerCommand(request.FirstName, request.LastName, request.Email, request.Password, request.PhoneNumber),
            cancellationToken);
        return CreatedAtAction(nameof(MeController.Get), "Me", null, CurrentUserResponse.From(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [EndpointDescription("Anonymous. Returns an access token; sets the HttpOnly refresh cookie and the CSRF cookie.")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        AuthCookies.Issue(Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(AuthSessionResponse.From(session));
    }

    [HttpPost("refresh")]
    [ValidateCsrf]
    [EndpointDescription("Anonymous (refresh cookie + X-XSRF-TOKEN header). Rotates the refresh token.")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RenewSessionCommand(Request.Cookies[AuthCookies.RefreshCookieName] ?? string.Empty), cancellationToken);
        if (result.Session is null)
        {
            AuthCookies.Clear(Response);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired.", detail: "Sign in again.");
        }

        AuthCookies.Issue(Response, result.Session.RefreshToken, result.Session.RefreshTokenExpiresAtUtc);
        return Ok(AuthSessionResponse.From(result.Session));
    }

    [HttpPost("logout")]
    [ValidateCsrf]
    [EndpointDescription("Anonymous (refresh cookie + X-XSRF-TOKEN header). Revokes the whole login and clears cookies.")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(Request.Cookies[AuthCookies.RefreshCookieName]), cancellationToken);
        AuthCookies.Clear(Response);
        return NoContent();
    }
}
