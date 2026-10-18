using Crm.Api.Contracts.Branding;
using Crm.Application.Branding.GetOrganizationLogo;
using Crm.Application.Branding.GetPublicBranding;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

/// <summary>PLAT-001 D7: anonymous, public branding only (the sign-in page needs it before any session exists).</summary>
[ApiController]
[AllowAnonymous]
[Route(ApiRoutes.V1 + "/branding")]
public sealed class BrandingController(ISender sender) : ControllerBase
{
    [EndpointDescription("Anonymous. Public branding for the sign-in page and shells: displayName (null = not configured), accentColor preset id (null = default), logoVersion (null = no logo; changes on every logo replacement). No support or account data.")]
    [HttpGet]
    public async Task<ActionResult<PublicBrandingResponse>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        return Ok(PublicBrandingResponse.From(await sender.Send(new GetPublicBrandingQuery(), cancellationToken)));
    }

    [EndpointDescription("Anonymous. The organization logo bytes (PNG, JPEG or WebP only). 404 when none. Use ?v=<logoVersion> for cache busting.")]
    [HttpGet("logo")]
    public async Task<IActionResult> Logo(CancellationToken cancellationToken)
    {
        var file = await sender.Send(new GetOrganizationLogoQuery(), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers.ContentDisposition = "inline";
        return File(file.Content, file.ContentType); // no file name ⇒ no attachment disposition
    }
}
