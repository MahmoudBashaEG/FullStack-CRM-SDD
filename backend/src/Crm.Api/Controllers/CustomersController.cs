using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Customers;
using Crm.Application.Common.Paging;
using Crm.Application.Customers.CreateCustomer;
using Crm.Application.Customers.GetCustomer;
using Crm.Application.Customers.ListCustomers;
using Crm.Application.Customers.UpdateCustomer;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/customers")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin or Agent. One-based page (default 1), pageSize (default 20, values above 100 are clamped to 100), optional search on name/email/phone. Newest first.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCustomersQuery(search, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<CustomerResponse>(
            result.Items.Select(CustomerResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent.")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(CustomerResponse.From(await sender.Send(new GetCustomerQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Creates a contact-only customer; no login account is created.")]
    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new CreateCustomerCommand(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Company),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, CustomerResponse.From(customer));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. A registered customer's email cannot change (422); their name/phone changes also update their account.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new UpdateCustomerCommand(id, request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Company),
            cancellationToken);
        return Ok(CustomerResponse.From(customer));
    }
}
