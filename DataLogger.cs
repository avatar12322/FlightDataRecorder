using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace FlightDataRecorder;

public sealed class DataLogger : IDisposable
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private CancellationTokenSource? _cts;
    private Task? _writerTask;
    private StreamWriter? _writer;

    public bool IsRunning => _writerTask is not null && !_writerTask.IsCompleted;
    public string? CurrentFilePath { get; private set; }

    public void Start(string filePath, string header)
    {
        if (_writerTask is not null)
        {
            throw new InvalidOperationException("Logger already started.");
        }

        _cts = new CancellationTokenSource();
        CurrentFilePath = filePath;
        _writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(false));
        _writer.WriteLine(header);
        _writerTask = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    public void Enqueue(string line)
    {
        if (_writer is null)
        {
            return;
        }

        _queue.Enqueue(line);
        _signal.Release();
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _signal.Release();

        if (_writerTask is not null)
        {
            try
            {
                await _writerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal termination path.
            }
        }

        if (_writer is not null)
        {
            await _writer.FlushAsync().ConfigureAwait(false);
            _writer.Dispose();
            _writer = null;
        }

        _writerTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task WriteLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await _signal.WaitAsync(token).ConfigureAwait(false);
            await FlushQueueAsync().ConfigureAwait(false);
        }

        await FlushQueueAsync().ConfigureAwait(false);
    }

    private async Task FlushQueueAsync()
    {
        if (_writer is null)
        {
            return;
        }

        while (_queue.TryDequeue(out string? line))
        {
            await _writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_writerTask is not null)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        _signal.Dispose();
        _cts?.Dispose();
        _writer?.Dispose();
    }
}
