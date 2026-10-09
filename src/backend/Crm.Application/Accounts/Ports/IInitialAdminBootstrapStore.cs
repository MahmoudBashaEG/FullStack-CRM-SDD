using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Common;
namespace Crm.Application.Accounts.Ports;

// Owns the installation transaction and requests input only if setup is incomplete.
public interface IInitialAdminBootstrapStore
{
    Task<OperationResult<AdminBootstrapResult>> BootstrapAsync(Func<CancellationToken,Task<OperationResult<NewAccount>>> input,CancellationToken cancellationToken);
}
