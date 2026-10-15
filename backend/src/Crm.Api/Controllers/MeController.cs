using Crm.Api.Authentication;
using Crm.Api.Contracts.Profile;
using Crm.Application.Profile.ChangeMyPassword;
using Crm.Application.Profile.GetMyProfile;
using Crm.Application.Profile.UpdateMyProfile;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/me")]
[Authorize]
public sealed class MeController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires: any authenticated user (own account only).")]
    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> Get(CancellationToken cancellationToken) =>
        Ok(CurrentUserResponse.From(await sender.Send(new GetMyProfileQuery(User.GetUserId()), cancellationToken)));

    [EndpointDescription("Requires: any authenticated user (own account only).")]
    [HttpPut]
    public async Task<ActionResult<CurrentUserResponse>> Update(UpdateMyProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await sender.Send(
            new UpdateMyProfileCommand(User.GetUserId(), request.FirstName, request.LastName, request.PhoneNumber),
            cancellationToken);
        return Ok(CurrentUserResponse.From(user));
    }

    [EndpointDescription("Requires: any authenticated user (own account only).")]
    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new ChangeMyPasswordCommand(User.GetUserId(), request.CurrentPassword, request.NewPassword), cancellationToken);
        AuthCookies.Clear(Response); // every session of this user was revoked; the client signs in again
        return NoContent();
    }
}
