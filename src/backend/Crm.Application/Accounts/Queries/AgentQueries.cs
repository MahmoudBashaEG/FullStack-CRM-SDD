using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Ports;
using Crm.Application.Common;
using MediatR;
using Crm.Application.Accounts.Validation;
namespace Crm.Application.Accounts.Queries;
public sealed record GetAgentQuery(AccountIdentity? Actor,Guid UserId):IRequest<OperationResult<AgentAccountDto>>;
public sealed record ListAgentsQuery(AccountIdentity? Actor,int Page=1,int PageSize=25):IRequest<OperationResult<AgentPage>>;
public sealed class AgentQueryHandler(IAgentAccountStore store):IRequestHandler<GetAgentQuery,OperationResult<AgentAccountDto>>,IRequestHandler<ListAgentsQuery,OperationResult<AgentPage>>
{
    public Task<OperationResult<AgentAccountDto>> Handle(GetAgentQuery request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(AgentManagementValidation.Authorize(request.Actor) is {} denied)return Task.FromResult(new OperationResult<AgentAccountDto>(null,denied));
        return request.UserId==Guid.Empty?Task.FromResult(OperationResult<AgentAccountDto>.Fail("validation_failed",400)):store.GetAsync(request.UserId,ct);
    }
    public async Task<OperationResult<AgentPage>> Handle(ListAgentsQuery request,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(AgentManagementValidation.Authorize(request.Actor) is {} denied)return new(null,denied);
        if(request.Page<1||request.PageSize is <1 or >100)return OperationResult<AgentPage>.Fail("validation_failed",400);
        return OperationResult<AgentPage>.Success(await store.ListAsync(request.Page,request.PageSize,ct));
    }
}
