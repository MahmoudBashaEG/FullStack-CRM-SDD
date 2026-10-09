using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Application.Common;
using MediatR;
using Crm.Application.Accounts.Validation;
using Crm.Domain.Accounts;
namespace Crm.Application.Authentication.Commands;

public sealed record SignInCommand(string? Email,string? Password,string? PriorCredential) : IRequest<OperationResult<SessionIssue>>;
public sealed record RefreshAuthenticationCommand(string? Credential) : IRequest<OperationResult<SessionIssue>>;
public sealed record SignOutCommand(string? Credential,TrustedAuthentication? Bearer) : IRequest<OperationResult<bool>>;
public sealed class SignInHandler(IAuthenticationOperations operations) : IRequestHandler<SignInCommand,OperationResult<SessionIssue>>
{
    public Task<OperationResult<SessionIssue>> Handle(SignInCommand request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(request.Password is null || !AccountValidation.TryNormalizeEmail(request.Email,out var email,out _))return Task.FromResult(OperationResult<SessionIssue>.Fail("validation_failed",400));
        if(!AccountRules.PasswordAccepted(request.Password))return Task.FromResult(OperationResult<SessionIssue>.Fail("invalid_credentials",401));
        return operations.SignInAsync(email,request.Password,request.PriorCredential,ct);
    }
}
public sealed class RefreshHandler(IAuthenticationOperations operations) : IRequestHandler<RefreshAuthenticationCommand,OperationResult<SessionIssue>>
{
    public Task<OperationResult<SessionIssue>> Handle(RefreshAuthenticationCommand request,CancellationToken ct){ct.ThrowIfCancellationRequested();return operations.RefreshAsync(request.Credential,ct);}
}
public sealed class SignOutHandler(IAuthenticationOperations operations) : IRequestHandler<SignOutCommand,OperationResult<bool>>
{
    public Task<OperationResult<bool>> Handle(SignOutCommand request,CancellationToken ct){ct.ThrowIfCancellationRequested();return operations.SignOutAsync(request.Credential,request.Bearer,ct);}
}
