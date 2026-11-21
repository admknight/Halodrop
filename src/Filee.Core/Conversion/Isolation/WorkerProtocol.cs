// Morph change: the messages between Morph and an engine worker process, one JSON object per line.
//
//   Morph → worker   run (a step), alloc (the answer to the worker's alloc), cancel
//   worker → Morph   ready (once, when its engines are built), alloc (asks for an output path), progress, notice,
//                    fonts (missing document fonts), done (the files written), refused (an error key), failed, cancelled
//
// Every line the worker writes starts with Marker: anything else on its standard output (a native library printing)
// is not a message and is only logged.

using System.Text.Json;
using System.Text.Json.Serialization;
using Filee.Core.Presets;
using Filee.Core.Settings;

namespace Filee.Core.Conversion.Isolation;

/// <summary>One message. Only the fields of its kind (<see cref="T"/>) are set.</summary>
public sealed class WorkerMessage
{
    public string T { get; set; } = "";
    public long Id { get; set; }
    public long Seq { get; set; }

    // run
    public string? Engine { get; set; }
    public string? Input { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Preset { get; set; }
    public string? Work { get; set; }
    public bool Frames { get; set; }
    public bool Motion { get; set; }
    public bool Live { get; set; }
    public string? Original { get; set; }
    public bool Caption { get; set; }
    public bool Translate { get; set; }

    // alloc (both ways)
    public string? Ext { get; set; }
    public string? Suffix { get; set; }
    public string? Path { get; set; }

    // progress, notice, fonts, done, refused, failed
    public double Value { get; set; }
    public string? Key { get; set; }
    public string? Detail { get; set; }
    public string? Message { get; set; }
    public List<string>? Names { get; set; }
    public List<string>? Outputs { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(WorkerMessage))]
internal sealed partial class WorkerJsonContext : JsonSerializerContext;

/// <summary>Reading and writing <see cref="WorkerMessage"/> lines.</summary>
public static class WorkerProtocol
{
    /// <summary>Starts every message line the worker writes.</summary>
    public const string Marker = "\u0001morph:";

    /// <summary>The command-line switch that starts Morph as an engine worker.</summary>
    public const string Switch = "--engine-worker";

    public static string Write(WorkerMessage message) => JsonSerializer.Serialize(message, WorkerJsonContext.Default.WorkerMessage);

    public static WorkerMessage? Read(string line)
    {
        try { return JsonSerializer.Deserialize(line, WorkerJsonContext.Default.WorkerMessage); }
        catch (JsonException) { return null; }
    }

    /// <summary>A preset as the worker receives it: the same JSON presets.json holds, so nothing is lost.</summary>
    public static string WritePreset(Preset preset) => JsonSerializer.Serialize([preset], FileeJsonContext.Default.ListPreset);

    public static Preset ReadPreset(string json) =>
        JsonSerializer.Deserialize(json, FileeJsonContext.Default.ListPreset) is [var preset] ? preset
            : throw new JsonException("A run message without exactly one preset.");
}
