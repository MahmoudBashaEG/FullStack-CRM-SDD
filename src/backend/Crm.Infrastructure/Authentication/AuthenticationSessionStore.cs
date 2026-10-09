using System.Data;
using System.Security.Cryptography;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Ports;
using Crm.Application.Common;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace Crm.Infrastructure.Authentication;

public sealed class AuthenticationSessionStore(CrmDbContext db,LocalAccountAuthenticator accounts,IAccessTokenIssuer issuer,TimeProvider clock) : IAuthenticationOperations,IRefreshSessionReader
{
    public async Task<Guid?> ReadLiveSessionAsync(string? credential,CancellationToken ct)
    {
        var reference=await LookupAsync(credential,ct);if(reference is null)return null;var now=clock.GetUtcNow();
        return await (from session in db.AuthenticationSessions.AsNoTracking() join user in db.Users.AsNoTracking() on session.UserId equals user.Id
            join token in db.RefreshCredentials.AsNoTracking() on session.Id equals token.SessionId
            where session.Id==reference.SessionId&&token.Id==reference.CredentialId&&token.ExpiresAtUtc>now&&user.IsActive&&user.AuthVersion==session.AuthVersionAtCreation&&session.ExpiresAtUtc>now&&session.RevokedAtUtc==null select (Guid?)session.Id).SingleOrDefaultAsync(ct);
    }
    private sealed record Reference(Guid CredentialId,Guid SessionId,Guid UserId,byte[] Digest);
    private static byte[]? Digest(string? value)
    {
        if(value is null||value.Length!=43)return null;
        try{var bytes=Base64UrlEncoder.DecodeBytes(value);return bytes.Length==32&&Base64UrlEncoder.Encode(bytes)==value?SHA256.HashData(bytes):null;}catch(FormatException){return null;}
    }
    private async Task<Reference?> LookupAsync(string? value,CancellationToken ct)
    {
        var hash=Digest(value);if(hash is null)return null;
        var found=await (from token in db.RefreshCredentials.AsNoTracking() join session in db.AuthenticationSessions.AsNoTracking() on token.SessionId equals session.Id where token.TokenHash==hash select new Reference(token.Id,session.Id,session.UserId,token.TokenHash)).SingleOrDefaultAsync(ct);
        return found is not null&&CryptographicOperations.FixedTimeEquals(hash,found.Digest)?found:null;
    }
    private Task<LocalUser?> LockUserAsync(Guid id,CancellationToken ct)=>db.Users.FromSqlInterpolated($"SELECT * FROM dbo.CrmUsers WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
    private Task<AuthenticationSession?> LockSessionAsync(Guid id,CancellationToken ct)=>db.AuthenticationSessions.FromSqlInterpolated($"SELECT * FROM dbo.AuthenticationSessions WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
    private Task<RefreshCredential?> LockCredentialAsync(Guid id,CancellationToken ct)=>db.RefreshCredentials.FromSqlInterpolated($"SELECT * FROM dbo.RefreshCredentials WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct);
    private bool Live(AuthenticationSession? session,LocalUser? user)=>session is not null&&user is {IsActive:true}&&session.RevokedAtUtc is null&&session.ExpiresAtUtc>clock.GetUtcNow()&&session.AuthVersionAtCreation==user.AuthVersion;
    private void Revoke(AuthenticationSession session,SessionRevocationReason reason){session.RevokedAtUtc=clock.GetUtcNow();session.RevocationReason=reason;}
    private (RefreshCredential Credential,string Raw) NewCredential(AuthenticationSession session)
    {
        var bytes=RandomNumberGenerator.GetBytes(32);return(new(){SessionId=session.Id,TokenHash=SHA256.HashData(bytes),CreatedAtUtc=clock.GetUtcNow(),ExpiresAtUtc=session.ExpiresAtUtc},Base64UrlEncoder.Encode(bytes));
    }
    private SessionIssue Issue(LocalUser user,AuthenticationSession session,string raw,CancellationToken ct)=>new(issuer.Issue(new(new(user.Id,user.DisplayName,user.AccountRole),session.Id,user.AuthVersion,session.ExpiresAtUtc),ct),raw,session.ExpiresAtUtc);
    public async Task<OperationResult<SessionIssue>> SignInAsync(string email,string password,string? priorCredential,CancellationToken ct)
    {
        var normalized=email.Normalize().ToUpperInvariant();var id=await db.Users.AsNoTracking().Where(x=>x.NormalizedEmail==normalized).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(ct);
        var prior=await LookupAsync(priorCredential,ct);
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        // A replacement may involve two accounts; lock their IDs in one deterministic order.
        var locked=new Dictionary<Guid,LocalUser?>();foreach(var accountId in new[]{id,prior?.UserId}.Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().Order())locked[accountId]=await LockUserAsync(accountId,ct);
        var user=id.HasValue?locked[id.Value]:null;
        if(!await accounts.VerifyAsync(user,password,ct))return OperationResult<SessionIssue>.Fail("invalid_credentials",401);
        if(prior is not null)
        {
            var old=await LockSessionAsync(prior.SessionId,ct);var credential=await LockCredentialAsync(prior.CredentialId,ct);
            if(Live(old,locked[prior.UserId])&&credential is {ConsumedAtUtc:null}&&credential.ExpiresAtUtc>clock.GetUtcNow())Revoke(old!,SessionRevocationReason.Replaced);
        }
        var now=clock.GetUtcNow();var session=new AuthenticationSession {UserId=user!.Id,AuthVersionAtCreation=user.AuthVersion,CreatedAtUtc=now,ExpiresAtUtc=now.AddDays(7)};
        var next=NewCredential(session);db.AuthenticationSessions.Add(session);db.RefreshCredentials.Add(next.Credential);
        var result=Issue(user,session,next.Raw,ct);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<SessionIssue>.Success(result);
    }
    public async Task<OperationResult<SessionIssue>> RefreshAsync(string? credential,CancellationToken ct)
    {
        var reference=await LookupAsync(credential,ct);if(reference is null)return OperationResult<SessionIssue>.Fail("authentication_invalid",401);
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        var user=await LockUserAsync(reference.UserId,ct);var session=await LockSessionAsync(reference.SessionId,ct);var old=await LockCredentialAsync(reference.CredentialId,ct);
        if(!Live(session,user)||old is null||old.ExpiresAtUtc<=clock.GetUtcNow())return OperationResult<SessionIssue>.Fail("authentication_invalid",401);
        if(old.ConsumedAtUtc is not null){Revoke(session!,SessionRevocationReason.CredentialReplay);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<SessionIssue>.Fail("authentication_invalid",401);}
        old.ConsumedAtUtc=clock.GetUtcNow();await db.SaveChangesAsync(ct); // Release filtered uniqueness before successor insert, in the same transaction.
        var next=NewCredential(session!);db.RefreshCredentials.Add(next.Credential);old.ReplacedById=next.Credential.Id;
        var result=Issue(user!,session!,next.Raw,ct);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<SessionIssue>.Success(result);
    }
    public async Task<OperationResult<bool>> SignOutAsync(string? credential,TrustedAuthentication? bearer,CancellationToken ct)
    {
        var reference=await LookupAsync(credential,ct);if(reference is null&&bearer is null)return OperationResult<bool>.Success(true);
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        var users=new Dictionary<Guid,LocalUser?>();foreach(var id in new[]{reference?.UserId,bearer?.Identity.UserId}.Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().Order())users[id]=await LockUserAsync(id,ct);
        var sessions=new Dictionary<Guid,AuthenticationSession?>();foreach(var id in new[]{reference?.SessionId,bearer?.SessionId}.Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().Order())sessions[id]=await LockSessionAsync(id,ct);
        var cookieSession=reference is null?null:sessions[reference.SessionId];var token=reference is null?null:await LockCredentialAsync(reference.CredentialId,ct);
        var cookieLive=reference is not null&&Live(cookieSession,users[reference.UserId])&&token is not null&&token.ExpiresAtUtc>clock.GetUtcNow();
        var bearerSession=bearer is null?null:sessions[bearer.SessionId];var bearerLive=bearer is not null&&Live(bearerSession,users[bearer.Identity.UserId])&&users[bearer.Identity.UserId]!.AuthVersion==bearer.AuthenticationVersion;
        if(cookieLive&&bearerLive&&cookieSession!.Id!=bearerSession!.Id)return OperationResult<bool>.Fail("authentication_context_changed",409);
        if(cookieLive)Revoke(cookieSession!,SessionRevocationReason.Signout);if(bearerLive)Revoke(bearerSession!,SessionRevocationReason.Signout);
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return OperationResult<bool>.Success(true);
    }
}
