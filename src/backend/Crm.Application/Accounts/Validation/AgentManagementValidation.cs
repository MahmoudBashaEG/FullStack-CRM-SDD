using Crm.Application.Accounts.Contracts;
using Crm.Application.Common;
using Crm.Domain.Accounts;
namespace Crm.Application.Accounts.Validation;
public static class AgentManagementValidation
{
    public static Failure? Authorize(AccountIdentity? actor)=>actor is null||actor.UserId==Guid.Empty?new("authentication_invalid",401):actor.Role!=AccountRole.Admin?new("forbidden",403):null;
    public static bool TryVersion(string? value,out byte[] bytes)
    {
        bytes=[];if(value is null)return false;
        try{bytes=Convert.FromBase64String(value);return bytes.Length==8&&Convert.ToBase64String(bytes)==value;}catch(FormatException){return false;}
    }
}
