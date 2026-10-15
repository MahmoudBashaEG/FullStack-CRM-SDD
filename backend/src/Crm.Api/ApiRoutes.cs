namespace Crm.Api;

/// <summary>Route prefixes. Business controllers use [Route(ApiRoutes.V1 + "/plural-resource")].</summary>
public static class ApiRoutes
{
    /// <summary>Infrastructure-only, intentionally unversioned.</summary>
    public const string Health = "api/health";

    /// <summary>Prefix for all business resources, e.g. "api/v1/tickets".</summary>
    public const string V1 = "api/v1";
}
