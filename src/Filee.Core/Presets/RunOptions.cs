// Morph change: "Run a program" (Preset.RunTool), the user's own action on the donut: a program or a command run with
// the dropped files, e.g. "Open in VS Code" = code {files}. Idea from OrbitDrop (MIT, github.com/i3sam/OrbitDrop),
// written for Morph and for all three systems. Only actions the user made run: they are never imported or shared.

namespace Filee.Core.Presets;

public sealed class RunOptions
{
    /// <summary>
    /// The program: a full path, a name found on the PATH ("code", "python"), or on a Mac an .app. Empty runs
    /// <see cref="Arguments"/> as a command (cmd on Windows, sh elsewhere).
    /// </summary>
    public string Program { get; set; } = "";

    /// <summary>What the program gets, with {file}, {files}, {folder} (each quoted), {name} and {ext}.</summary>
    public string Arguments { get; set; } = "{files}";

    /// <summary>Once for each file (true), or once with all of them (false).</summary>
    public bool PerFile { get; set; }

    /// <summary>Shows the program's console window (to read a script's output); off runs consoles hidden.</summary>
    public bool ShowWindow { get; set; }

    /// <summary>Only for these extensions ("psd ai", dots and commas optional); empty for every file and folder.</summary>
    public string Extensions { get; set; } = "";

    public RunOptions Clone() => (RunOptions)MemberwiseClone();

    /// <summary>The extensions of <see cref="Extensions"/>, lower case without the dot.</summary>
    public IReadOnlySet<string> ExtensionSet() =>
        Extensions.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Whether the action is offered for <paramref name="path"/>: every file and folder, or its extensions.</summary>
    public bool Accepts(string path)
    {
        var set = ExtensionSet();
        if (set.Count == 0)
            return true;
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension.Length > 0 && set.Contains(extension);
    }

    /// <summary>What a problem with the action is (a text key), or null when it can run.</summary>
    public string? Problem() =>
        string.IsNullOrWhiteSpace(Program) && string.IsNullOrWhiteSpace(Arguments) ? "run.problem_nothing" : null;

    /// <summary>
    /// <see cref="Arguments"/> for <paramref name="files"/> (the first is {file}). {file}, {files} and {folder} come
    /// quoted, so names with spaces, &amp; or quotes arrive whole.
    /// </summary>
    public string Expand(IReadOnlyList<string> files, bool windows)
    {
        var first = files.Count > 0 ? files[0] : "";
        var folder = Directory.Exists(first) ? first : Path.GetDirectoryName(first.TrimEnd('\\', '/')) ?? first;
        // One pass over the placeholders: a file name that itself contains "{name}" must stay as it is.
        var result = new System.Text.StringBuilder();
        var text = Arguments;
        for (var i = 0; i < text.Length;)
        {
            var replaced = false;
            foreach (var (token, value) in new (string, Func<string>)[]
                     {
                         ("{files}", () => string.Join(" ", files.Select(f => Quote(f, windows)))),
                         ("{file}", () => Quote(first, windows)),
                         ("{folder}", () => Quote(folder, windows)),
                         ("{name}", () => Path.GetFileNameWithoutExtension(first.TrimEnd('\\', '/'))),
                         ("{ext}", () => Path.GetExtension(first).TrimStart('.')),
                     })
            {
                if (string.Compare(text, i, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    continue;
                result.Append(value());
                i += token.Length;
                replaced = true;
                break;
            }
            if (!replaced)
                result.Append(text[i++]);
        }
        return result.ToString();
    }

    /// <summary>
    /// A path in double quotes. Windows: a trailing backslash is doubled so it can't escape the quote (and paths can't
    /// hold quotes). Elsewhere a POSIX shell quote: inside double quotes only $ ` " \ need a backslash.
    /// </summary>
    public static string Quote(string path, bool windows)
    {
        if (windows)
            return "\"" + (path.EndsWith('\\') ? path + "\\" : path) + "\"";
        var escaped = new System.Text.StringBuilder(path.Length + 2).Append('"');
        foreach (var c in path)
        {
            if (c is '$' or '`' or '"' or '\\')
                escaped.Append('\\');
            escaped.Append(c);
        }
        return escaped.Append('"').ToString();
    }

    /// <summary>
    /// What to start for <paramref name="expanded"/> arguments. Windows: an .exe or .com runs directly; scripts on the
    /// PATH (VS Code's "code" is code.cmd) and plain commands go through cmd (/k keeps the window open when
    /// <see cref="ShowWindow"/> asks). Elsewhere everything runs through sh with the line as one argument, so the POSIX
    /// quoting of <see cref="Quote"/> holds; a Mac .app opens with "open -a".
    /// </summary>
    public RunCommand CommandLine(string expanded, bool windows, bool mac, Func<string, string?> resolve)
    {
        var program = Program.Trim().Trim('"');
        if (windows)
        {
            var found = program.Length == 0 ? null : resolve(program);
            var direct = found is not null && (found.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || found.EndsWith(".com", StringComparison.OrdinalIgnoreCase));
            if (direct && !ShowWindow)
                return new RunCommand(found!, expanded, []);
            var line = program.Length == 0 ? expanded : $"{Quote(found ?? program, true)} {expanded}".TrimEnd();
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            return new RunCommand(cmd, $"/d /s /{(ShowWindow ? "k" : "c")} \"{line}\"", []);
        }
        var shellLine = program.Length == 0 ? expanded
            : mac && program.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? $"open -a {Quote(program, false)} {expanded}".TrimEnd()
            : $"{Quote(program, false)} {expanded}".TrimEnd();
        return new RunCommand("/bin/sh", null, ["-c", shellLine]);
    }

    /// <summary>A program's full path: as given, or found on the PATH (on Windows with .exe, .com, .cmd or .bat); null if absent.</summary>
    public static string? Resolve(string program, bool windows)
    {
        if (Path.IsPathRooted(program))
            return File.Exists(program) || Directory.Exists(program) ? program : null;
        string[] extensions = windows && !Path.HasExtension(program) ? [".exe", ".com", ".cmd", ".bat"] : [""];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim().Trim('"'), program + extension);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException) { } // a malformed PATH entry
            }
        }
        return null;
    }
}

/// <summary>A process to start: raw <see cref="Arguments"/> (Windows) or an <see cref="ArgumentList"/> (elsewhere).</summary>
public sealed record RunCommand(string FileName, string? Arguments, IReadOnlyList<string> ArgumentList);
