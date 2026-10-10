using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using MediatR;
using Crm.Application.Accounts.Validation;
using Crm.Domain.Accounts;
namespace Crm.Application.Accounts.Commands;
public sealed record CreateAgentCommand(AccountIdentity? Actor,string? DisplayName,string? Email,string? Password):IRequest<OperationResult<AgentAccountDto>>;
public sealed record RenameAgentCommand(AccountIdentity? Actor,Guid UserId,string? DisplayName,string? Version):IRequest<OperationResult<AgentAccountDto>>;
public sealed record SetAgentAvailabilityCommand(AccountIdentity? Actor,Guid UserId,bool IsActive,string? Version):IRequest<OperationResult<AgentAccountDto>>;
public sealed class AgentCommandHandler(IAgentAccountStore store):IRequestHandler<CreateAgentCommand,OperationResult<AgentAccountDto>>,IRequestHandler<RenameAgentCommand,OperationResult<AgentAccountDto>>,IRequestHandler<SetAgentAvailabilityCommand,OperationResult<AgentAccountDto>>
{
    public Task<OperationResult<AgentAccountDto>> Handle(CreateAgentCommand request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(AgentManagementValidation.Authorize(request.Actor) is {} denied)return Task.FromResult(new OperationResult<AgentAccountDto>(null,denied));
        Dictionary<string,string[]> errors=[];
        if(!AccountRules.DisplayNameAccepted(request.DisplayName))errors["displayName"]=["A nonblank display name of at most 200 characters is required."];
        if(!AccountValidation.TryNormalizeEmail(request.Email,out var email,out _))errors["email"]=["A valid email of at most 256 characters is required."];
        if(!AccountRules.PasswordAccepted(request.Password))errors["password"]=["Password must contain 15–128 characters; spaces and Unicode are allowed."];
        return errors.Count>0?Task.FromResult(OperationResult<AgentAccountDto>.Fail("validation_failed",400,errors)):store.CreateAsync(new(request.DisplayName!.Trim(),email,request.Password!,AccountRole.Agent),ct);
    }
    public Task<OperationResult<AgentAccountDto>> Handle(RenameAgentCommand request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(AgentManagementValidation.Authorize(request.Actor) is {} denied)return Task.FromResult(new OperationResult<AgentAccountDto>(null,denied));
        if(request.UserId==Guid.Empty||!AccountRules.DisplayNameAccepted(request.DisplayName)||!AgentManagementValidation.TryVersion(request.Version,out var version))return Task.FromResult(OperationResult<AgentAccountDto>.Fail("validation_failed",400));
        return store.RenameAsync(request.UserId,request.DisplayName!.Trim(),version,ct);
    }
    public Task<OperationResult<AgentAccountDto>> Handle(SetAgentAvailabilityCommand request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(AgentManagementValidation.Authorize(request.Actor) is {} denied)return Task.FromResult(new OperationResult<AgentAccountDto>(null,denied));
        if(request.UserId==Guid.Empty||!AgentManagementValidation.TryVersion(request.Version,out var version))return Task.FromResult(OperationResult<AgentAccountDto>.Fail("validation_failed",400));
        return store.SetAvailabilityAsync(request.UserId,request.IsActive,version,ct);
    }
}
