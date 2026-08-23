using System.Threading;
using System.Threading.Tasks;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Core.Interfaces;

public interface IInternetConnectivityTester
{
    Task<string> RunTestAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default);
}
