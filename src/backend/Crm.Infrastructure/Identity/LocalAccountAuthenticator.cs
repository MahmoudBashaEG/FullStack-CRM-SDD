using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
namespace Crm.Infrastructure.Identity;

public sealed class DummyPasswordHash
{
    public LocalUser User {get;}=new();
    public string Hash {get;}
    public DummyPasswordHash(Microsoft.Extensions.Options.IOptions<PasswordHasherOptions> options)=>Hash=new PasswordHasher<LocalUser>(options).HashPassword(User,Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
}
public sealed class LocalAccountAuthenticator(UserManager<LocalUser> users,DummyPasswordHash dummy)
{
    public async Task<bool> VerifyAsync(LocalUser? user,string password,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();bool verified;
        if(user is null){_ = users.PasswordHasher.VerifyHashedPassword(dummy.User,dummy.Hash,password);verified=false;}
        else verified=await users.CheckPasswordAsync(user,password);
        ct.ThrowIfCancellationRequested();return verified && user is {IsActive:true};
    }
}
