// Morph change: the worker side of engine isolation. Morph starts its own executable with WorkerProtocol.Switch; that
// process builds the engines and runs here: it reads run messages from standard input, runs one step at a time and
// writes the replies. Output paths are asked of Morph (its allocator knows the user's naming and conflict rules), so
// the worker writes exactly where an in-process engine would. Standard input closing means Morph is gone: the worker
// ends too.

using System.Collections.Concurrent;

namespace Filee.Core.Conversion.Isolation;

public sealed class EngineWorker
{
    private readonly IReadOnlyDictionary<string, IConverter> _engines;
    private readonly TextWriter _out;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<string?>> _allocations = new();
    private long _seq;
    private CancellationTokenSource? _running;
    private long _runningId;
    private readonly Action? _beforeStep;

    /// <param name="engines">The engines by id.</param>
    /// <param name="output">Where messages go (the real standard output; everything else is pointed elsewhere).</param>
    /// <param name="beforeStep">Runs before each step (the host picks up engine settings changed meanwhile).</param>
    public EngineWorker(IEnumerable<IConverter> engines, TextWriter output, Action? beforeStep = null)
    {
        _beforeStep = beforeStep;
        _engines = engines.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        _out = output;
    }

    /// <summary>Tests only (MORPH_WORKER_TESTS=1): an engine id that ends the worker the hard way, like a native crash.</summary>
    public const string CrashForTests = "__crash";

    /// <summary>Serves until <paramref name="input"/> ends. Returns the exit code.</summary>
    public async Task<int> RunAsync(TextReader input)
    {
        Send(new WorkerMessage { T = "ready" });
        Task? step = null;
        while (await input.ReadLineAsync() is { } line)
        {
            if (WorkerProtocol.Read(line) is not { } message)
                continue;
            switch (message.T)
            {
                case "run":
                    // One step at a time: Morph sends the next only after this one's answer.
                    if (step is { IsCompleted: false })
                        await step;
                    step = Task.Run(() => RunStepAsync(message));
                    break;
                case "alloc":
                    if (_allocations.TryRemove(message.Seq, out var waiting))
                        waiting.TrySetResult(message.Path);
                    break;
                case "cancel":
                    if (Interlocked.Read(ref _runningId) == message.Id)
                        _running?.Cancel();
                    break;
            }
        }
        // Morph closed the pipe: stop what runs and leave.
        _running?.Cancel();
        foreach (var waiting in _allocations.Values)
            waiting.TrySetResult(null);
        if (step is not null)
            await Task.WhenAny(step, Task.Delay(TimeSpan.FromSeconds(5)));
        return 0;
    }

    private async Task RunStepAsync(WorkerMessage run)
    {
        var id = run.Id;
        using var cancel = new CancellationTokenSource();
        _running = cancel;
        Interlocked.Exchange(ref _runningId, id);
        try
        {
            if (run.Engine == CrashForTests && Environment.GetEnvironmentVariable("MORPH_WORKER_TESTS") == "1")
                Environment.FailFast("Engine worker crash requested by a test.");
            _beforeStep?.Invoke();
            if (run.Engine is null || !_engines.TryGetValue(run.Engine, out var engine))
                throw new InvalidOperationException($"The worker has no engine '{run.Engine}'.");

            var step = new ConversionStep(
                run.Input!, run.From!, run.To!, WorkerProtocol.ReadPreset(run.Preset!), new RemoteAllocator(this, id), run.Work!,
                run.Frames,
                key => Send(new WorkerMessage { T = "notice", Id = id, Key = key }),
                run.Motion, run.Live,
                fonts => Send(new WorkerMessage { T = "fonts", Id = id, Names = [.. fonts] }),
                run.Original, run.Caption, run.Translate);
            var outputs = await engine.ConvertAsync(step, new ThrottledProgress(this, id), cancel.Token);
            Send(new WorkerMessage { T = "done", Id = id, Outputs = [.. outputs] });
        }
        catch (OperationCanceledException)
        {
            Send(new WorkerMessage { T = "cancelled", Id = id });
        }
        catch (ConversionRefusedException ex)
        {
            Send(new WorkerMessage { T = "refused", Id = id, Key = ex.ErrorKey, Detail = ex.Detail });
        }
        catch (Exception ex)
        {
            Send(new WorkerMessage { T = "failed", Id = id, Message = ex.Message });
        }
        finally
        {
            Interlocked.Exchange(ref _runningId, 0);
            _running = null;
        }
    }

    private void Send(WorkerMessage message)
    {
        var line = WorkerProtocol.Marker + WorkerProtocol.Write(message);
        lock (_writeLock)
        {
            _out.WriteLine(line);
            _out.Flush();
        }
    }

    /// <summary>Asks Morph for an output path and waits for the answer (the engine calls this on its own thread).</summary>
    private string? Allocate(long id, string extension, string? suffix)
    {
        var seq = Interlocked.Increment(ref _seq);
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _allocations[seq] = answer;
        Send(new WorkerMessage { T = "alloc", Id = id, Seq = seq, Ext = extension, Suffix = suffix });
        return answer.Task.GetAwaiter().GetResult();
    }

    private sealed class RemoteAllocator(EngineWorker worker, long id) : IOutputAllocator
    {
        public string? Allocate(string extension, string? suffix = null) => worker.Allocate(id, extension, suffix);
    }

    /// <summary>Progress at most ten times a second, and always the end.</summary>
    private sealed class ThrottledProgress(EngineWorker worker, long id) : IProgress<double>
    {
        private long _last;

        public void Report(double value)
        {
            var now = Environment.TickCount64;
            if (value < 1 && now - Interlocked.Read(ref _last) < 100)
                return;
            Interlocked.Exchange(ref _last, now);
            worker.Send(new WorkerMessage { T = "progress", Id = id, Value = value });
        }
    }
}
