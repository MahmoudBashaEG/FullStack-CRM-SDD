using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using MediatR;
using Crm.Application.Accounts.Validation;
using Crm.Domain.Accounts;
namespace Crm.Application.Accounts.Commands;

public sealed record RegisterCustomerCommand(string? DisplayName, string? Email, string? Password) : IRequest<OperationResult<AccountIdentity>>;
public sealed class RegisterCustomerHandler(IAccountRegistrationStore store) : IRequestHandler<RegisterCustomerCommand, OperationResult<AccountIdentity>>
{
    public Task<OperationResult<AccountIdentity>> Handle(RegisterCustomerCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Dictionary<string,string[]> errors=[];
        if(!AccountRules.DisplayNameAccepted(request.DisplayName))errors["displayName"]=["A nonblank display name of at most 200 characters is required."];
        if(!AccountValidation.TryNormalizeEmail(request.Email,out var email,out _))errors["email"]=["A valid email of at most 256 characters is required."];
        if(!AccountRules.PasswordAccepted(request.Password))errors["password"]=["Password must contain 15–128 characters; spaces and Unicode are allowed."];
        if(errors.Count>0)return Task.FromResult(OperationResult<AccountIdentity>.Fail("validation_failed",400,errors));
        return store.RegisterAsync(new(request.DisplayName!.Trim(),email,request.Password!,AccountRole.Customer),ct);
    }
}
