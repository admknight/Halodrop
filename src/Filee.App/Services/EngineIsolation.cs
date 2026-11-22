// Morph change: whether this Morph runs its in-process engines in worker processes (IsolatedStepRunner), and the runner.
// On for Morph itself; off where the executable isn't Morph (tests run in a test host, which can't be a worker) and
// when MORPH_ENGINES_IN_PROCESS=1 asks for the old way (troubleshooting).

using Filee.Core.Conversion.Isolation;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed class EngineIsolation : IDisposable
{
    public EngineIsolation(string dataDirectory, ILogger<IsolatedStepRunner> log, string? program = null)
    {
        program ??= Environment.ProcessPath;
        if (program is not null && IsEnabled(program))
            Runner = new IsolatedStepRunner(program, EngineWorkerHost.Arguments(dataDirectory), log);
    }

    /// <summary>The runner, or null when engines run in this process.</summary>
    public IsolatedStepRunner? Runner { get; }

    internal static bool IsEnabled(string program) =>
        Environment.GetEnvironmentVariable("MORPH_ENGINES_IN_PROCESS") != "1"
        && string.Equals(Path.GetFileNameWithoutExtension(program), "Halodrop", StringComparison.OrdinalIgnoreCase);

    public void Dispose() => Runner?.Dispose();
}
