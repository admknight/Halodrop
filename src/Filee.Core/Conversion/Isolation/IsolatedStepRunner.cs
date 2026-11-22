// Morph change: runs the engines that live inside Morph's process (ImageMagick, PDFium, the HWP, CAD, font and office
// engines...) in worker processes instead. A native crash in one of them used to close Morph with every conversion in
// it; now it ends only that worker: the file it was converting fails with "error.engine_crashed", the next step gets a
// fresh worker, and Morph keeps running. Engines that already run a program of their own (FFmpeg, LibreOffice ...)
// stay as they are.
//
// Workers are kept warm and reused one step at a time (starting one costs a moment and loads the native libraries);
// those left idle for a minute end. If no worker can be started at all, steps run in this process as before: isolation
// never stops a conversion that would otherwise work.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.Core.Conversion.Isolation;

public sealed class IsolatedStepRunner : IStepRunner, IDisposable
{
    /// <summary>The engines that run inside Morph's own process: the ones a worker takes.</summary>
    public static readonly IReadOnlySet<string> InProcessEngines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "magick", "pdfium", "pdfsharp", "rhwp", "unhwp", "vector", "font", "cad", "model3d", "image3d", "icns", "gcode",
        "geo", "ebook", "email", "p7m", "hwpx-writer", "docx-writer", "pdf-text", "pdf-slides", "pdf-tables",
        "document-tables", "ooxml", "spreadsheet", "data", "subtitle", "markdown",
    };

    private readonly string _program;
    private readonly IReadOnlyList<string> _arguments;
    private readonly IReadOnlySet<string> _isolated;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentStack<Worker> _idle = new();
    private readonly ConcurrentDictionary<Worker, byte> _all = new();
    private readonly Timer _trim;
    private readonly TimeSpan _idleTimeout;
    private long _nextId;
    /// <summary>When starting a worker last failed (Environment.TickCount64), or -1.</summary>
    private long _startFailedAt = -1;
    private bool _disposed;

    /// <param name="program">The executable to start (Morph itself).</param>
    /// <param name="arguments">Its arguments: <see cref="WorkerProtocol.Switch"/> and the data folder.</param>
    /// <param name="isolated">Engine ids to run in workers; <see cref="InProcessEngines"/> when null.</param>
    /// <param name="maxWorkers">Workers at the same time; four at most by default (each loads the native libraries).</param>
    /// <param name="memoryCeiling">
    /// Morph change: the most memory a worker may hold while it converts (bytes); past it the worker is stopped and the
    /// file fails with "error.engine_memory". <see cref="DefaultMemoryCeiling"/> when 0.
    /// </param>
    /// <param name="stallLimit">
    /// Morph change: how long a step may go without a word from its worker (progress, an output, a notice) before it's
    /// stopped as stuck ("error.engine_stalled"). <see cref="DefaultStallLimit"/> when null.
    /// </param>
    public IsolatedStepRunner(string program, IReadOnlyList<string> arguments, ILogger? log = null, IReadOnlySet<string>? isolated = null,
        int maxWorkers = 0, TimeSpan? idleTimeout = null, long memoryCeiling = 0, TimeSpan? stallLimit = null)
    {
        _memoryCeiling = memoryCeiling > 0 ? memoryCeiling : DefaultMemoryCeiling();
        _stallLimit = stallLimit ?? DefaultStallLimit;
        _program = program;
        _arguments = arguments;
        _isolated = isolated ?? InProcessEngines;
        _log = log ?? NullLogger.Instance;
        _slots = new SemaphoreSlim(maxWorkers > 0 ? maxWorkers : Math.Clamp(Environment.ProcessorCount, 1, 4));
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(1);
        _trim = new Timer(_ => TrimIdle(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
    }

    private int _started;

    // ---- Morph change: limits (an idea from OrbitDrop, MIT) ----

    private readonly long _memoryCeiling;
    private readonly TimeSpan _stallLimit;

    /// <summary>A step that says nothing for this long is taken as stuck. Long videos report progress all along.</summary>
    public static readonly TimeSpan DefaultStallLimit = TimeSpan.FromMinutes(15);

    /// <summary>60 % of the computer's memory, at least 2 GB: room for big pictures, not enough to make the PC crawl.</summary>
    public static long DefaultMemoryCeiling()
    {
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return Math.Max(2L << 30, total > 0 ? (long)(total * 0.6) : 4L << 30);
    }

    /// <summary>How often a working worker's memory and silence are looked at.</summary>
    public static TimeSpan WatchEvery { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Workers started so far.</summary>
    public int Started => Volatile.Read(ref _started);

    /// <summary>Processes running now (tests).</summary>
    public int LiveWorkers => _all.Count;

    public async Task<IReadOnlyList<string>> RunAsync(IConverter converter, ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (_disposed || !_isolated.Contains(converter.Id) || RecentlyFailedToStart())
            return await converter.ConvertAsync(step, progress, cancellationToken);

        await _slots.WaitAsync(cancellationToken);
        Worker? worker = null;
        try
        {
            worker = TakeIdle() ?? await StartAsync(cancellationToken);
            if (worker is null)
                return await converter.ConvertAsync(step, progress, cancellationToken);
            return await worker.RunAsync(Interlocked.Increment(ref _nextId), converter, step, progress, cancellationToken);
        }
        finally
        {
            if (worker is not null)
            {
                if (worker.IsAlive && !_disposed)
                    _idle.Push(worker);
                else
                    Forget(worker);
            }
            _slots.Release();
        }
    }

    private bool RecentlyFailedToStart()
    {
        var failedAt = Interlocked.Read(ref _startFailedAt);
        return failedAt >= 0 && Environment.TickCount64 - failedAt < TimeSpan.FromMinutes(1).TotalMilliseconds;
    }

    private Worker? TakeIdle()
    {
        while (_idle.TryPop(out var worker))
        {
            if (worker.IsAlive)
                return worker;
            Forget(worker);
        }
        return null;
    }

    private async Task<Worker?> StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var worker = await Worker.StartAsync(_program, _arguments, _log, cancellationToken);
            worker.MemoryCeiling = _memoryCeiling;
            worker.StallLimit = _stallLimit;
            _all[worker] = 0;
            Interlocked.Increment(ref _started);
            return worker;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never worse than before isolation: this step and those of the next minute run in Morph's process.
            Interlocked.Exchange(ref _startFailedAt, Environment.TickCount64);
            _log.LogWarning(ex, "Could not start an engine worker ({Program}); engines run in Halodrop's process for now", _program);
            return null;
        }
    }

    private void TrimIdle()
    {
        var keep = new List<Worker>();
        while (_idle.TryPop(out var worker))
        {
            if (worker.IsAlive && worker.IdleFor < _idleTimeout && !_disposed)
                keep.Add(worker);
            else
                Forget(worker);
        }
        for (var i = keep.Count - 1; i >= 0; i--)
            _idle.Push(keep[i]);
    }

    private void Forget(Worker worker)
    {
        _all.TryRemove(worker, out _);
        worker.Dispose();
    }

    public void Dispose()
    {
        _disposed = true;
        _trim.Dispose();
        foreach (var worker in _all.Keys)
            worker.Dispose();
        _all.Clear();
        _idle.Clear();
    }

    /// <summary>One worker process.</summary>
    private sealed class Worker : IDisposable
    {
        private readonly Process _process;
        private readonly ILogger _log;
        private readonly object _writeLock = new();
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Request? _current;
        private long _idleSince = Environment.TickCount64;

        // Morph change: the limits, and when the worker last said something about the step it runs.
        public long MemoryCeiling { get; set; } = long.MaxValue;
        public TimeSpan StallLimit { get; set; } = Timeout.InfiniteTimeSpan;
        private long _heardAt = Environment.TickCount64;
        private volatile string? _stoppedFor;
        private long _peakMemory;

        private Worker(Process process, ILogger log)
        {
            _process = process;
            _log = log;
        }

        public bool IsAlive => !_exited.Task.IsCompleted;

        public TimeSpan IdleFor => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _idleSince));

        public static async Task<Worker> StartAsync(string program, IReadOnlyList<string> arguments, ILogger log, CancellationToken cancellationToken)
        {
            var info = new ProcessStartInfo(program)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardInputEncoding = new System.Text.UTF8Encoding(false),
            };
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            var worker = new Worker(process, log);
            process.OutputDataReceived += (_, e) => worker.OnLine(e.Data);
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    log.LogDebug("Engine worker: {Line}", e.Data);
            };
            process.Exited += (_, _) => worker.OnExited();
            if (!process.Start())
                throw new InvalidOperationException("The engine worker did not start.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Building the engines takes a moment; a worker that doesn't get there in time is no use.
            var ready = await Task.WhenAny(worker._ready.Task, worker._exited.Task, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            if (ready != worker._ready.Task)
            {
                worker.Dispose();
                throw new InvalidOperationException("The engine worker did not get ready.");
            }
            return worker;
        }

        public async Task<IReadOnlyList<string>> RunAsync(long id, IConverter converter, ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            var request = new Request(id, step, progress);
            _current = request;
            Interlocked.Exchange(ref _heardAt, Environment.TickCount64);
            _stoppedFor = null;
            Interlocked.Exchange(ref _peakMemory, 0);
            using var watch = new Timer(_ => Watch(request), null, WatchEvery, WatchEvery);
            try
            {
                Send(new WorkerMessage
                {
                    T = "run",
                    Id = id,
                    Engine = converter.Id,
                    Input = step.InputPath,
                    From = step.From,
                    To = step.To,
                    Preset = WorkerProtocol.WritePreset(step.Preset),
                    Work = step.WorkDirectory,
                    Frames = step.FrameSequence,
                    Motion = step.MotionPhoto,
                    Live = step.LivePhoto,
                    Original = step.OriginalSourcePath,
                    Caption = step.Caption,
                    Translate = step.Translate,
                });
                using var cancel = cancellationToken.Register(() =>
                {
                    TrySend(new WorkerMessage { T = "cancel", Id = id });
                    // An engine that doesn't stop when asked is stopped with its worker.
                    _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                    {
                        if (!request.Answer.Task.IsCompleted)
                            Kill();
                    }, TaskScheduler.Default);
                });
                var finished = await Task.WhenAny(request.Answer.Task, _exited.Task);
                if (finished != request.Answer.Task && !request.Answer.Task.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Morph change: stopped by a limit, said as such.
                    if (_stoppedFor == "memory")
                        throw new ConversionRefusedException("error.engine_memory",
                            $"{converter.DisplayName}, {Interlocked.Read(ref _peakMemory) >> 20} MB (limit {MemoryCeiling >> 20} MB)");
                    if (_stoppedFor == "stall")
                        throw new ConversionRefusedException("error.engine_stalled", $"{converter.DisplayName}, {StallLimit.TotalMinutes:0} min");
                    var code = SafeExitCode();
                    _log.LogWarning("The engine worker stopped while {Engine} converted {File} (exit code {Code})", converter.Id, step.InputPath, code);
                    throw new ConversionRefusedException("error.engine_crashed", $"{converter.DisplayName}, exit code {code}");
                }
                return await request.Answer.Task;
            }
            finally
            {
                _current = null;
                Interlocked.Exchange(ref _idleSince, Environment.TickCount64);
            }
        }

        /// <summary>
        /// Morph change: stops a worker that holds more memory than allowed or has said nothing for too long. Its
        /// engines run on: they're stopped with it (the whole process tree).
        /// </summary>
        private void Watch(Request request)
        {
            if (_current != request || _stoppedFor is not null || !IsAlive)
                return;
            long memory;
            try
            {
                _process.Refresh();
                memory = _process.WorkingSet64;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                return;
            }
            if (memory > Interlocked.Read(ref _peakMemory))
                Interlocked.Exchange(ref _peakMemory, memory);
            var silent = TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _heardAt));
            var reason = memory > MemoryCeiling ? "memory" : StallLimit != Timeout.InfiniteTimeSpan && silent > StallLimit ? "stall" : null;
            if (reason is null)
                return;
            _stoppedFor = reason;
            _log.LogWarning("Stopping an engine worker on {File}: {Reason} ({Memory} MB, silent {Silent})", request.Step.InputPath, reason,
                memory >> 20, silent);
            Kill();
        }

        private void OnLine(string? line)
        {
            if (line is null)
                return;
            Interlocked.Exchange(ref _heardAt, Environment.TickCount64);
            if (!line.StartsWith(WorkerProtocol.Marker, StringComparison.Ordinal))
            {
                if (line.Length > 0)
                    _log.LogDebug("Engine worker printed: {Line}", line);
                return;
            }
            if (WorkerProtocol.Read(line[WorkerProtocol.Marker.Length..]) is not { } message)
                return;
            if (message.T == "ready")
            {
                _ready.TrySetResult();
                return;
            }
            if (_current is not { } request || request.Id != message.Id)
                return;
            switch (message.T)
            {
                case "alloc":
                    string? path;
                    try { path = request.Step.Output.Allocate(message.Ext ?? "", message.Suffix); }
                    catch (Exception ex) { _log.LogWarning(ex, "Allocating an output for the engine worker failed"); path = null; }
                    TrySend(new WorkerMessage { T = "alloc", Id = message.Id, Seq = message.Seq, Path = path });
                    break;
                case "progress":
                    request.Progress?.Report(message.Value);
                    break;
                case "notice":
                    if (message.Key is not null)
                        request.Step.Notice?.Invoke(message.Key);
                    break;
                case "fonts":
                    if (message.Names is { Count: > 0 } names)
                        request.Step.MissingFonts?.Invoke(names);
                    break;
                case "done":
                    request.Answer.TrySetResult(message.Outputs ?? []);
                    break;
                case "refused":
                    request.Answer.TrySetException(new ConversionRefusedException(message.Key ?? "error.conversion_failed", message.Detail));
                    break;
                case "failed":
                    request.Answer.TrySetException(new EngineWorkerException(message.Message ?? "The engine failed."));
                    break;
                case "cancelled":
                    request.Answer.TrySetCanceled();
                    break;
            }
        }

        private void OnExited() => _exited.TrySetResult();

        private void Send(WorkerMessage message)
        {
            lock (_writeLock)
            {
                _process.StandardInput.WriteLine(WorkerProtocol.Write(message));
                _process.StandardInput.Flush();
            }
        }

        private void TrySend(WorkerMessage message)
        {
            try { Send(message); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
        }

        private int? SafeExitCode()
        {
            try { return _process.HasExited ? _process.ExitCode : null; }
            catch (InvalidOperationException) { return null; }
        }

        private void Kill()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        }

        public void Dispose()
        {
            // Closing its input ends a worker on its own; one that doesn't is stopped.
            try { _process.StandardInput.Close(); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
            if (!_process.WaitForExit(500))
                Kill();
            _process.Dispose();
        }

        private sealed record Request(long Id, ConversionStep Step, IProgress<double>? Progress)
        {
            public TaskCompletionSource<IReadOnlyList<string>> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

/// <summary>An engine in a worker process failed; its message is the engine's own.</summary>
public sealed class EngineWorkerException(string message) : Exception(message);
