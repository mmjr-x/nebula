using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace STUN.Proxy;

public interface ITcpProxy : IDisposable
{
	IPEndPoint? CurrentLocalEndPoint { get; }

	ValueTask<IDuplexPipe> ConnectAsync(IPEndPoint local, IPEndPoint dst, CancellationToken cancellationToken = default);
	ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
