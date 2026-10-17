using Crm.Api.Authentication;
using Crm.Api.Contracts.Customers;
using Crm.Api.Contracts.Profile;
using Crm.Application.Profile.ChangeMyPassword;
using Crm.Application.Profile.GetMyCustomerProfile;
using Crm.Application.Profile.GetMyProfile;
using Crm.Application.Profile.UpdateMyCustomerProfile;
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

    [EndpointDescription("Requires role: Customer (own linked CRM profile only).")]
    [Authorize(Policy = AuthorizationPolicies.Customer)]
    [HttpGet("customer-profile")]
    public async Task<ActionResult<CustomerResponse>> GetCustomerProfile(CancellationToken cancellationToken) =>
        Ok(CustomerResponse.From(await sender.Send(new GetMyCustomerProfileQuery(User.GetUserId()), cancellationToken)));

    [EndpointDescription("Requires role: Customer (own linked CRM profile only). Email is not editable; name/phone also update the account.")]
    [Authorize(Policy = AuthorizationPolicies.Customer)]
    [HttpPut("customer-profile")]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomerProfile(
        UpdateMyCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new UpdateMyCustomerProfileCommand(User.GetUserId(), request.FirstName, request.LastName, request.PhoneNumber, request.Company),
            cancellationToken);
        return Ok(CustomerResponse.From(customer));
    }
}
