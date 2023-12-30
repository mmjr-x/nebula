using Microsoft;
//using Pipelines.Extensions;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Validation;

namespace STUN.Proxy;

public class DuplexPipe : IDuplexPipe
{
    public PipeReader Input { get; }
    public PipeWriter Output { get; }

    public DuplexPipe(PipeReader input, PipeWriter output)
    {
        Input = input;
        Output = output;
    }
}


public class DirectTcpProxy : ITcpProxy/*, IDisposableObservable*/
{
	public IPEndPoint? CurrentLocalEndPoint
	{
		get
		{
			//Verify.NotDisposed(this);
			return TcpClient?.Client.LocalEndPoint as IPEndPoint;
		}
	}

	protected TcpClient? TcpClient;

	public virtual async ValueTask<IDuplexPipe> ConnectAsync(IPEndPoint local, IPEndPoint dst, CancellationToken cancellationToken = default)
	{
        //Verify.NotDisposed(this);
        Requires.NotNull(local, nameof(local));
        Requires.NotNull(dst, nameof(dst));

        await CloseAsync(cancellationToken);

		TcpClient = new TcpClient(local) { NoDelay = true };
		//await TcpClient.ConnectAsync(dst, cancellationToken);
        await TcpClient.ConnectAsync(dst.Address, dst.Port);

        var pipeOptions = new PipeOptions(
            readerScheduler: PipeScheduler.ThreadPool,
            writerScheduler: PipeScheduler.ThreadPool,
            pauseWriterThreshold: 1024,
            resumeWriterThreshold: 512,
            minimumSegmentSize: 512,
            useSynchronizationContext: false);

        var pipe = new Pipe(pipeOptions);

        // Start separate tasks for reading and writing
        var networkStream = TcpClient.GetStream();
        _ = ReadDataAsync(networkStream, pipe.Writer);
        _ = WriteDataAsync(networkStream, pipe.Reader);

        return new DuplexPipe(pipe.Reader, pipe.Writer);

        //return TcpClient.Client.AsDuplexPipe();
    }

	public ValueTask CloseAsync(CancellationToken cancellationToken = default)
	{
		//Verify.NotDisposed(this);

		CloseClient();

		return default;
	}

	protected virtual void CloseClient()
	{
		if (TcpClient is null)
		{
			return;
		}

		try
		{
			TcpClient.Client.Close(0);
		}
		finally
		{
			TcpClient.Dispose();
			TcpClient = default;
		}
	}

	public bool IsDisposed { get; private set; }

	public void Dispose()
	{
		IsDisposed = true;

		CloseClient();

		GC.SuppressFinalize(this);
	}

    static async Task ReadDataAsync(NetworkStream networkStream, PipeWriter writer)
    {
        try
        {
            while (true)
            {
                Memory<byte> buffer = writer.GetMemory(512); // Adjust the buffer size as needed
                int bytesRead = await networkStream.ReadAsync(buffer.ToArray(), 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break; // End of stream
                }

                writer.Advance(bytesRead);

                // Notify the reader that new data is available
                await writer.FlushAsync();
            }

            // Mark the writer as complete
            writer.Complete();
        }
        catch (Exception ex)
        {
            // Handle exceptions
            Console.WriteLine($"ReadDataAsync error: {ex.Message}");
            writer.Complete(ex);
        }
    }

    static async Task WriteDataAsync(NetworkStream networkStream, PipeReader reader)
    {
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (result.IsCompleted)
                {
                    break; // End of stream
                }

                // Write the data to the network stream
                await networkStream.WriteAsync(buffer.ToArray(), 0, (int)buffer.Length);

                // Mark the data as consumed
                reader.AdvanceTo(buffer.End);
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions
            Console.WriteLine($"WriteDataAsync error: {ex.Message}");
            reader.Complete(ex);
        }
    }
}
