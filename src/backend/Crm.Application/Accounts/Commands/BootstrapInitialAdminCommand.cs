using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using MediatR;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Validation;
using Crm.Domain.Accounts;
namespace Crm.Application.Accounts.Commands;

public sealed record AdminBootstrapCredentials(string? DisplayName,string? Email,string? Password);
public sealed record AdminBootstrapResult(string Status,Guid UserId);
public sealed record BootstrapInitialAdminCommand(Func<CancellationToken,Task<AdminBootstrapCredentials?>> ReadCredentials) : IRequest<OperationResult<AdminBootstrapResult>>;
public sealed class BootstrapInitialAdminHandler(IInitialAdminBootstrapStore store) : IRequestHandler<BootstrapInitialAdminCommand,OperationResult<AdminBootstrapResult>>
{
    public Task<OperationResult<AdminBootstrapResult>> Handle(BootstrapInitialAdminCommand request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return store.BootstrapAsync(async ct =>
        {
            var input = await request.ReadCredentials(ct);
            ct.ThrowIfCancellationRequested();
            if(input is null || !AccountRules.DisplayNameAccepted(input.DisplayName) || !AccountRules.PasswordAccepted(input.Password)
                || !AccountValidation.TryNormalizeEmail(input.Email,out var email,out _))
                return OperationResult<NewAccount>.Fail("bootstrap_invalid",400);
            return OperationResult<NewAccount>.Success(new(input.DisplayName!.Trim(),email,input.Password!,AccountRole.Admin));
        },cancellationToken);
    }
}
