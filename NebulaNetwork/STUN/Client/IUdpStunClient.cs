using System;
using System.Threading;
using System.Threading.Tasks;

namespace STUN.Client;

public interface IUdpStunClient : IStunClient
{
	TimeSpan ReceiveTimeout { get; set; }
	ValueTask ConnectProxyAsync(CancellationToken cancellationToken = default);
	ValueTask CloseProxyAsync(CancellationToken cancellationToken = default);
}
