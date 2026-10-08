namespace Crm.Application.Common;

public sealed record Actor(string Issuer, string Subject, string DisplayName);
public interface ICurrentActor { Actor Get(); }
