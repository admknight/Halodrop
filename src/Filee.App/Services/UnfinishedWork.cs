// Morph change: conversions that didn't finish (Morph crashed, the computer went off, Morph was quit mid-way) can be
// picked up again. While a job runs, its files that aren't done yet, its preset (as the worker protocol writes it,
// so nothing is lost) and the file being converted are kept in unfinished.json in the data folder; each finished file
// leaves it, and so does the whole job when it ends. At the next start, what is left is offered again
// (ResumeNoticeWindow).
//
// A file that was being converted when Morph stopped twice in a row is left out of the next try and named instead,
// so one broken file can't make Morph fall over again and again. Files from a browser (not saved yet) and from watch
// folders (which pick their files up again themselves) aren't kept.

using System.Text.Json;
using System.Text.Json.Serialization;
using Filee.Core.Conversion;
using Filee.Core.Conversion.Isolation;
using Filee.Core.Presets;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed class UnfinishedWork : IDisposable
{
    /// <summary>A file in progress while Morph stopped this many times is left out.</summary>
    public const int SuspectLimit = 2;

    private readonly string _path;
    private readonly JobQueue? _queue;
    private readonly Func<string, bool> _fromWatchFolder;
    private readonly ILogger? _log;
    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _jobs = new();
    private readonly Dictionary<string, int> _suspects;

    /// <param name="dataDirectory">Where unfinished.json lives.</param>
    /// <param name="queue">The jobs to follow; null only reads what an earlier run left (tests).</param>
    /// <param name="fromWatchFolder">True for a job a watch folder started.</param>
    public UnfinishedWork(string dataDirectory, JobQueue? queue, Func<string, bool>? fromWatchFolder = null, ILogger<UnfinishedWork>? log = null)
    {
        _path = Path.Combine(dataDirectory, "unfinished.json");
        _queue = queue;
        _fromWatchFolder = fromWatchFolder ?? (_ => false);
        _log = log;
        Left = Read();
        // Files that were being converted when the last run stopped: one strike more.
        _suspects = new Dictionary<string, int>(Left.Suspects, StringComparer.OrdinalIgnoreCase);
        var counted = false;
        foreach (var job in Left.Jobs)
        {
            if (job.Running is not { } running)
                continue;
            _suspects[running] = _suspects.GetValueOrDefault(running) + 1;
            // Counted once: a later start, after a run that ended well, mustn't count it again.
            job.Running = null;
            counted = true;
        }
        if (counted)
            lock (_lock)
                Save();
        if (queue is not null)
        {
            queue.JobUpdated += OnUpdated;
            queue.JobFinished += OnFinished;
        }
    }

    /// <summary>What the last run left unfinished, read at start.</summary>
    public Snapshot Left { get; private set; }

    /// <summary>The files to convert again, by job, without those that stopped Morph too often.</summary>
    public IReadOnlyList<Resumable> ToResume() =>
        [.. Left.Jobs.Select(job => new Resumable(job, [.. job.Files.Where(f => !IsSuspect(f) && File.Exists(f))]))
            .Where(r => r.Files.Count > 0)];

    /// <summary>Files left out because Morph stopped while converting each of them <see cref="SuspectLimit"/> times.</summary>
    public IReadOnlyList<string> LeftOut =>
        [.. Left.Jobs.SelectMany(job => job.Files).Where(IsSuspect).Distinct(StringComparer.OrdinalIgnoreCase)];

    private bool IsSuspect(string file) => _suspects.GetValueOrDefault(file) >= SuspectLimit;

    /// <summary>The user answered: forget what the last run left (resumed jobs are followed again as new ones).</summary>
    public void Forget()
    {
        lock (_lock)
        {
            Left = new Snapshot();
            Save();
        }
    }

    /// <summary>The preset a resumed job runs with.</summary>
    public static Preset PresetOf(Entry job) => WorkerProtocol.ReadPreset(job.Preset);

    private void OnUpdated(object? sender, ConversionJob job)
    {
        if (job.Fetcher is not null)
            return;
        var pending = job.Files.Where(f => f.State is FileState.Pending or FileState.Running).Select(f => f.SourcePath).ToList();
        var running = job.Files.FirstOrDefault(f => f.State == FileState.Running)?.SourcePath;
        lock (_lock)
        {
            if (pending.Count == 0 || _fromWatchFolder(job.Id))
            {
                if (_jobs.Remove(job.Id))
                    Save();
                return;
            }
            if (_jobs.TryGetValue(job.Id, out var known) && known.Running == running && known.Files.SequenceEqual(pending))
                return;
            // A job that makes one file of all of them (merges) is done again whole, never with only its last files.
            var combines = job.Preset.Pdf.MergeIntoSingle || job.Preset.JoinsIntoOneMedia || job.Preset.JoinsImages
                           || job.Preset.PacksIntoOneArchive || job.Preset.Image.MultiPageTiff;
            _jobs[job.Id] = new Entry
            {
                Name = job.PresetDisplayName,
                Preset = known?.Preset ?? WorkerProtocol.WritePreset(job.Preset),
                Files = combines ? [.. job.Sources] : pending,
                Running = running,
            };
            Save();
        }
    }

    private void OnFinished(object? sender, ConversionJob job)
    {
        lock (_lock)
        {
            var changed = _jobs.Remove(job.Id);
            // Converted, or failed on its own: no longer a suspect.
            foreach (var file in job.Files)
                changed |= _suspects.Remove(file.SourcePath);
            if (changed)
                Save();
        }
    }

    private Snapshot Read()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize(File.ReadAllText(_path), UnfinishedJsonContext.Default.Snapshot) ?? new Snapshot();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _log?.LogWarning(ex, "Could not read {Path}", _path);
        }
        return new Snapshot();
    }

    /// <summary>Writes this run's jobs, what the last run left until it is answered, and the suspects. Under _lock.</summary>
    private void Save()
    {
        var snapshot = new Snapshot
        {
            Jobs = [.. Left.Jobs, .. _jobs.Values],
            Suspects = new Dictionary<string, int>(_suspects.Where(s => s.Value > 0), StringComparer.OrdinalIgnoreCase),
        };
        try
        {
            if (snapshot.Jobs.Count == 0 && snapshot.Suspects.Count == 0)
            {
                File.Delete(_path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, UnfinishedJsonContext.Default.Snapshot));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.LogWarning(ex, "Could not write {Path}", _path);
        }
    }

    public void Dispose()
    {
        if (_queue is null)
            return;
        _queue.JobUpdated -= OnUpdated;
        _queue.JobFinished -= OnFinished;
    }

    /// <summary>One job's files still to do.</summary>
    public sealed class Entry
    {
        public string Name { get; set; } = "";
        public string Preset { get; set; } = "";
        public List<string> Files { get; set; } = [];
        public string? Running { get; set; }
    }

    public sealed class Snapshot
    {
        public List<Entry> Jobs { get; set; } = [];
        public Dictionary<string, int> Suspects { get; set; } = [];
    }

    public sealed record Resumable(Entry Job, IReadOnlyList<string> Files);
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UnfinishedWork.Snapshot))]
internal sealed partial class UnfinishedJsonContext : JsonSerializerContext;
