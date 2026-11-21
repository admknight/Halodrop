// Morph change: the file actions (Preset.FileActionTools), run when their slice is picked. Each does one thing at once
// and says what it did in a short card (QuickNoticeWindow): copy the paths or names, show the file in its folder, open
// a terminal there, copy the text of pictures and PDFs (OCR), make a picture the wallpaper, or run the user's own
// program. Ideas from OrbitDrop (MIT, github.com/i3sam/OrbitDrop), written for Morph on Windows, macOS and Linux.
// Nothing here writes or changes the user's files; the wallpaper and copied text go through Morph's own folders.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Filee.App.Views;
using Filee.Core.Conversion;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed partial class FileActions(UserDataStore store, ILocalizer loc, IPlatformServices platform, ConversionService conversions,
    ThemeService theme, ILogger<FileActions> log)
{
    /// <summary>How many folders "Show in folder" opens at most (one per folder of the files).</summary>
    private const int MaxRevealed = 5;

    /// <summary>The conversion behind "Copy text": text recognised (OCR) into a TXT, as "Searchable PDF" reads.</summary>
    public static Preset CopyTextConversion(string? language = null) => new()
    {
        Id = "action-copy-text-run",
        Name = "Copy text",
        TargetFormat = "txt",
        Pdf = { Ocr = true, OcrLanguage = language ?? "eng" },
        Output = { Location = OutputLocation.CustomFolder },
    };

    /// <summary>Runs <paramref name="preset"/>'s action on <paramref name="files"/>.</summary>
    public Task RunAsync(IReadOnlyList<string> files, Preset preset)
    {
        log.LogInformation("File action {Tool} on {Count} file(s)", preset.Tool, files.Count);
        return preset.Tool switch
        {
            Preset.CopyPathTool => CopyAsync(string.Join(Environment.NewLine, files), "action.copied_paths", files.Count),
            Preset.CopyNameTool => CopyAsync(string.Join(Environment.NewLine, files.Select(f => Path.GetFileName(f.TrimEnd('\\', '/')))), "action.copied_names", files.Count),
            Preset.RevealTool => Reveal(files),
            Preset.TerminalTool => Terminal(files),
            Preset.WallpaperTool => WallpaperAsync(files),
            Preset.CopyTextTool => CopyTextAsync(files),
            Preset.RunTool => RunProgramAsync(files, preset),
            _ => Task.CompletedTask,
        };
    }

    // ---- the card and the clipboard ----

    private QuickNoticeWindow Notice(string title, string? detail = null, bool problem = false)
    {
        // Some actions finish off the UI thread (the OCR runs in the background); windows are made on it.
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(() => Notice(title, detail, problem));
        var window = new QuickNoticeWindow(title, detail, problem);
        theme.ApplyWindowClass(window);
        window.Show();
        return window;
    }

    private async Task CopyAsync(string text, string doneKey, int count)
    {
        var card = Notice(loc.Format(doneKey, count), Shorten(text));
        if (card.Clipboard is not { } clipboard || !await TrySetTextAsync(clipboard, text))
            card.Close();
    }

    private async Task<bool> TrySetTextAsync(IClipboard clipboard, string text)
    {
        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or ExternalException)
        {
            log.LogWarning(ex, "Could not put text on the clipboard");
            Notice(loc["action.clipboard_failed"], null, problem: true);
            return false;
        }
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";

    // ---- show in folder ----

    private Task Reveal(IReadOnlyList<string> files)
    {
        foreach (var file in files.GroupBy(f => Path.GetDirectoryName(f.TrimEnd('\\', '/')) ?? f, FileSystemPaths.Comparer)
                     .Select(g => g.First()).Take(MaxRevealed))
            platform.RevealInFileManager(file);
        return Task.CompletedTask;
    }

    // ---- terminal ----

    private Task Terminal(IReadOnlyList<string> files)
    {
        var first = files[0];
        var folder = Directory.Exists(first) ? first : Path.GetDirectoryName(first.TrimEnd('\\', '/')) ?? first;
        foreach (var start in TerminalCandidates(folder))
        {
            try
            {
                using var process = Process.Start(start);
                if (process is not null)
                    return Task.CompletedTask;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                log.LogDebug(ex, "No terminal through {Program}", start.FileName);
            }
        }
        Notice(loc["action.no_terminal"], null, problem: true);
        return Task.CompletedTask;
    }

    /// <summary>The terminals to try for <paramref name="folder"/>, the system's own first.</summary>
    internal static IEnumerable<ProcessStartInfo> TerminalCandidates(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows Terminal when it's there (an app alias in WindowsApps), else PowerShell in a console of its own.
            yield return Info("wt.exe", ["-d", folder], folder);
            yield return new ProcessStartInfo("powershell.exe") { WorkingDirectory = folder, UseShellExecute = true };
            yield break;
        }
        if (OperatingSystem.IsMacOS())
        {
            yield return Info("/usr/bin/open", ["-a", "Terminal", folder], folder);
            yield break;
        }
        // Linux: the distribution's choice first, then the common desktops' own terminals.
        yield return Info("x-terminal-emulator", [], folder);
        yield return Info("gnome-terminal", ["--working-directory=" + folder], folder);
        yield return Info("konsole", ["--workdir", folder], folder);
        yield return Info("xfce4-terminal", ["--working-directory=" + folder], folder);
        yield return Info("kgx", [], folder);
        yield return Info("kitty", ["--directory", folder], folder);
        yield return Info("alacritty", ["--working-directory", folder], folder);
        yield return Info("xterm", [], folder);
    }

    private static ProcessStartInfo Info(string program, IEnumerable<string> arguments, string folder)
    {
        var info = new ProcessStartInfo(program) { UseShellExecute = false, WorkingDirectory = folder };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return info;
    }

    // ---- copy text (OCR) ----

    private async Task CopyTextAsync(IReadOnlyList<string> files)
    {
        var folder = Path.Combine(store.Directory, "copied-text", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        try
        {
            // The language the user's "Searchable PDF" reads, so their own language is recognised.
            var language = store.FindPreset("searchable-pdf")?.Pdf.OcrLanguage;
            var conversion = CopyTextConversion(language);
            conversion.Output.CustomFolder = folder;
            var sources = files.Where(f => !Directory.Exists(f)).ToList();
            var job = await conversions.RunAsync(sources, conversion);
            var texts = job?.Outputs.Where(File.Exists).Select(File.ReadAllText).Select(t => t.Trim()).Where(t => t.Length > 0).ToList() ?? [];
            if (texts.Count == 0)
            {
                Notice(loc["action.no_text"], null, problem: true);
                return;
            }
            var text = string.Join(Environment.NewLine + Environment.NewLine, texts);
            await Dispatcher.UIThread.InvokeAsync(() => CopyAsync(text, "action.copied_text", text.Length));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    // ---- wallpaper ----

    private async Task WallpaperAsync(IReadOnlyList<string> files)
    {
        var source = files[0];
        // The system keeps the picture's path: a copy in Morph's folder stays when the original moves, and anything the
        // system can't show (HEIC, WebP, RAW ...) becomes a PNG first.
        var folder = Path.Combine(store.Directory, "wallpaper");
        Directory.CreateDirectory(folder);
        string? picture;
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".bmp")
        {
            picture = Path.Combine(folder, "wallpaper-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + extension);
            File.Copy(source, picture, overwrite: true);
        }
        else
        {
            var toPng = new Preset
            {
                Id = "action-wallpaper-run",
                Name = "Wallpaper",
                TargetFormat = "png",
                Output = { Location = OutputLocation.CustomFolder, CustomFolder = folder, FileNamePattern = "wallpaper-{date}-{time}" },
            };
            var job = await conversions.RunAsync([source], toPng);
            picture = job?.Outputs.FirstOrDefault(File.Exists);
        }
        var problem = picture is null ? loc["error.conversion_failed"] : SetWallpaper(picture);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (problem is null)
                Notice(loc["action.wallpaper_set"]);
            else
                Notice(loc["action.wallpaper_failed"], problem, problem: true);
        });
        // Older copies go: only the one on the desktop is kept.
        foreach (var old in Directory.GetFiles(folder).Where(f => !string.Equals(f, picture, StringComparison.Ordinal)))
        {
            try { File.Delete(old); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Makes <paramref name="picture"/> the desktop background; null when it worked, else why not.</summary>
    private string? SetWallpaper(string picture)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return SystemParametersInfoW(SpiSetDeskWallpaper, 0, picture, SpifUpdateIniFile | SpifSendChange)
                    ? null : $"Windows refused it (error {Marshal.GetLastWin32Error()})";
            if (OperatingSystem.IsMacOS())
                return RunQuietly("/usr/bin/osascript", ["-e", $"tell application \"System Events\" to tell every desktop to set picture to POSIX file \"{picture.Replace("\"", "\\\"")}\""]);
            var desktop = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").ToUpperInvariant();
            if (desktop.Contains("KDE"))
                return RunQuietly("plasma-apply-wallpaperimage", [picture]);
            var uri = new Uri(picture).AbsoluteUri;
            // GNOME and its relatives keep one picture for the light and one for the dark style.
            return RunQuietly("gsettings", ["set", "org.gnome.desktop.background", "picture-uri", uri])
                   ?? RunQuietly("gsettings", ["set", "org.gnome.desktop.background", "picture-uri-dark", uri]);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UriFormatException)
        {
            log.LogWarning(ex, "Setting the wallpaper failed");
            return ex.Message;
        }
    }

    /// <summary>Runs a program and waits for it (10 s at most); null when it ended well, else what it said.</summary>
    private static string? RunQuietly(string program, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{program} did not start");
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
            return $"{program} did not finish";
        return process.ExitCode == 0 ? null : (error.Result.Trim() is { Length: > 0 } said ? said : $"{program}: exit code {process.ExitCode}");
    }

    private const uint SpiSetDeskWallpaper = 0x0014;
    private const uint SpifUpdateIniFile = 0x01;
    private const uint SpifSendChange = 0x02;

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoW(uint action, uint param, string value, uint winIni);

    // ---- the user's own program ----

    private async Task RunProgramAsync(IReadOnlyList<string> files, Preset preset)
    {
        if (preset.Run is not { } run || run.Problem() is not null)
            return;
        var name = loc.DisplayName(preset);
        var accepted = files.Where(run.Accepts).ToList();
        IEnumerable<IReadOnlyList<string>> runs = run.PerFile ? accepted.Select(f => (IReadOnlyList<string>)[f]) : [accepted];
        foreach (var group in runs.Where(r => r.Count > 0))
        {
            var problem = await LaunchAsync(run, group);
            if (problem is not null)
            {
                Notice(loc.Format("run.failed", name), problem, problem: true);
                return;
            }
        }
    }

    /// <summary>Starts the program for <paramref name="files"/>; null when it started (and, run hidden, didn't fail at once).</summary>
    private async Task<string?> LaunchAsync(RunOptions run, IReadOnlyList<string> files)
    {
        var windows = OperatingSystem.IsWindows();
        var command = run.CommandLine(run.Expand(files, windows), windows, OperatingSystem.IsMacOS(), p => RunOptions.Resolve(p, windows));
        var folder = Path.GetDirectoryName(files[0].TrimEnd('\\', '/'));
        var info = new ProcessStartInfo(command.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = !run.ShowWindow,
            WorkingDirectory = folder is not null && Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        if (command.Arguments is not null)
            info.Arguments = command.Arguments;
        foreach (var argument in command.ArgumentList)
            info.ArgumentList.Add(argument);
        try
        {
            using var process = Process.Start(info);
            if (process is null)
                return loc["run.not_started"];
            if (run.ShowWindow)
                return null; // its window stays for the user to read
            // A hidden run that ends at once with an error ("not found", a typo) is reported; one that keeps running (a
            // program that opened) is fine.
            using var watch = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));
            try
            {
                await process.WaitForExitAsync(watch.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            return process.ExitCode == 0 ? null : loc.Format("run.exit_code", process.ExitCode);
        }
        catch (Win32Exception ex)
        {
            log.LogWarning(ex, "Could not start {Program}", command.FileName);
            return loc["run.not_started"];
        }
    }
}
