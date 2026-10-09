using System.Text;
using System.Text.Json;
using Crm.Application.Accounts.Commands;
namespace Crm.Api.Installation;

public static class AdminBootstrapInput
{
    public static async Task<AdminBootstrapCredentials?> ReadAsync(bool privateStdin,CancellationToken cancellationToken)
    {
        if(privateStdin)
        {
            var buffer=new char[1024];var text=new StringBuilder();int count;
            while((count=await Console.In.ReadAsync(buffer.AsMemory(),cancellationToken))!=0)
            {
                if(text.Length+count>16384)throw new JsonException();
                text.Append(buffer,0,count);
            }
            using var document=JsonDocument.Parse(text.ToString());
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new JsonException();
            var fields=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var property in document.RootElement.EnumerateObject())
            {
                if(property.Name is not ("displayName" or "email" or "password") || property.Value.ValueKind!=JsonValueKind.String
                    || !fields.TryAdd(property.Name,property.Value.GetString()!))throw new JsonException();
            }
            if(fields.Count!=3)throw new JsonException();
            return new(fields["displayName"],fields["email"],fields["password"]);
        }
        // Redirected input must explicitly opt into the private JSON protocol.
        if(Console.IsInputRedirected)throw new JsonException();
        Console.Error.Write("Display name: ");var name=Console.ReadLine();
        cancellationToken.ThrowIfCancellationRequested();
        Console.Error.Write("Email: ");var email=Console.ReadLine();
        Console.Error.Write("Password (hidden): ");
        var password=new StringBuilder();var previous=Console.TreatControlCAsInput;
        try
        {
            Console.TreatControlCAsInput=true;
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key=Console.ReadKey(intercept:true);
                if(key.Key==ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))throw new OperationCanceledException();
                if(key.Key==ConsoleKey.Enter)break;
                if(key.Key==ConsoleKey.Backspace)
                {
                    if(password.Length>0){var remove=char.IsLowSurrogate(password[^1]) && password.Length>1 && char.IsHighSurrogate(password[^2])?2:1;password.Length-=remove;}
                }
                else if(key.KeyChar!='\0')
                {
                    // Fail oversized input, never silently truncate it.
                    if(password.Length>=256)throw new JsonException();
                    password.Append(key.KeyChar);
                }
            }
        }
        finally{Console.TreatControlCAsInput=previous;Console.Error.WriteLine();}
        return new(name,email,password.ToString());
    }
}
