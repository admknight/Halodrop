// Morph change: how JobQueue runs one engine step (IConverter.ConvertAsync). In this process by default; the app runs
// the engines that live inside its own process in a worker process instead (Isolation/IsolatedStepRunner).

namespace Filee.Core.Conversion;

/// <summary>Runs one <see cref="ConversionStep"/> with an engine.</summary>
public interface IStepRunner
{
    /// <summary>Same contract as <see cref="IConverter.ConvertAsync"/>: the files written, or an exception.</summary>
    Task<IReadOnlyList<string>> RunAsync(IConverter converter, ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken);
}
