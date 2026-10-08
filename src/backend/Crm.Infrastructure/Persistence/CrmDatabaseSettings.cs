using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Crm.Infrastructure.Persistence;

public static class CrmDatabaseSettings
{
    public static string Load(IConfiguration configuration) => Validate(configuration.GetConnectionString("Crm") ?? throw new InvalidOperationException("ConnectionStrings:Crm is required."));
    public static string Validate(string text)
    {
        if (Regex.IsMatch(text, @"(?:^|;)\s*(?:user\s*id|uid|user|password|pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidOperationException("SQL credential keys are unsupported, including empty values.");
        var raw = new DbConnectionStringBuilder { ConnectionString = text };
        foreach (string key in raw.Keys)
            if (new[] { "user id", "uid", "user", "password", "pwd" }.Contains(key.ToLowerInvariant()))
                throw new InvalidOperationException("SQL credentials are unsupported.");
        var builder = new SqlConnectionStringBuilder(text);
        var server = builder.DataSource;
        var host = server.Split('\\', ',')[0];
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) host = host[4..];
        if (!builder.IntegratedSecurity || builder.Authentication != SqlAuthenticationMethod.NotSpecified || string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
            !new[] { "localhost", ".", "(local)", Environment.MachineName, "127.0.0.1", "::1" }.Contains(host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("A local Integrated Security server and catalog are required.");
        if (!raw.ContainsKey("Application Name") && !raw.ContainsKey("app")) builder.ApplicationName = "Crm";
        return builder.ConnectionString;
    }
}
