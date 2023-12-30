using System;
using System.Threading;
using System.Threading.Tasks;

namespace STUN.Client;

public interface IStunClient : IDisposable
{
	ValueTask QueryAsync(CancellationToken cancellationToken = default);
}
