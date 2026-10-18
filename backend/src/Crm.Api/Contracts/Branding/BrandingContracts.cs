using System.Globalization;
using Crm.Application.Administration;

namespace Crm.Api.Contracts.Branding;

/// <summary>Public, anonymous: ONLY these three fields (PLAT-001 D7). logoVersion null = no logo; it changes on every logo replacement.</summary>
public sealed record PublicBrandingResponse(string? DisplayName, string? AccentColor, string? LogoVersion)
{
    public static PublicBrandingResponse From(PublicBrandingDetails d) =>
        new(d.DisplayName, d.AccentColor, d.LogoVersion?.ToString(CultureInfo.InvariantCulture));
}
