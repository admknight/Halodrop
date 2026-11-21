// Morph change: the apps installed for the user, for "Run a program" (an idea from OrbitDrop's app catalogue, MIT).
// Each system lists them its own way; what they have in common is read here so it's tested on any machine: Linux
// desktop entries, macOS .app bundles, Windows Start-menu shortcuts. Uninstallers, help files and hidden entries stay out.

using Filee.Core.Platform;

namespace Filee.Core.Morph;

public static class InstalledApps
{
    /// <summary>The most apps listed: a Start menu with thousands of entries mustn't make the list unusable.</summary>
    public const int MaxApps = 600;

    /// <summary>Sorted by name, once per name, at most <see cref="MaxApps"/>.</summary>
    public static IReadOnlyList<InstalledApp> Tidy(IEnumerable<InstalledApp> apps) =>
    [
        .. apps.Where(a => a.Name.Trim().Length > 0 && a.Program.Trim().Length > 0 && !Unwanted(a.Name))
            .GroupBy(a => a.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First() with { Name = g.Key })
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).Take(MaxApps),
    ];

    private static bool Unwanted(string name) =>
        name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) || name.Contains("readme", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(" help", StringComparison.OrdinalIgnoreCase) || name.Contains("release notes", StringComparison.OrdinalIgnoreCase);

    // ---- Linux: freedesktop desktop entries ----

    /// <summary>The application in a .desktop file's text, or null (hidden, not an app, no command).</summary>
    public static InstalledApp? FromDesktopEntry(string text)
    {
        string? name = null, exec = null, type = null;
        var hidden = false;
        var inEntry = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inEntry = line == "[Desktop Entry]";
                continue;
            }
            if (!inEntry || line.StartsWith('#') || line.IndexOf('=') is not (> 0 and var at))
                continue;
            var (key, value) = (line[..at].Trim(), line[(at + 1)..].Trim());
            switch (key)
            {
                case "Name": name ??= value; break;
                case "Exec": exec = value; break;
                case "Type": type = value; break;
                case "NoDisplay" or "Hidden" when value.Equals("true", StringComparison.OrdinalIgnoreCase): hidden = true; break;
            }
        }
        if (hidden || type != "Application" || name is null || exec is null)
            return null;
        var command = StripFieldCodes(exec);
        return command.Length == 0 ? null : new InstalledApp(name, command);
    }

    /// <summary>An Exec line without its field codes (%f %F %u %U ...): Morph adds the files itself.</summary>
    public static string StripFieldCodes(string exec)
    {
        var parts = exec.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !(p.Length == 2 && p[0] == '%') && !p.StartsWith("@@", StringComparison.Ordinal)) // Flatpak's file markers
            .Select(p => p.Replace("%%", "%", StringComparison.Ordinal));
        return string.Join(' ', parts);
    }

    /// <summary>Every desktop entry in <paramref name="folders"/> (later folders don't replace earlier ones' names).</summary>
    public static IReadOnlyList<InstalledApp> FromDesktopFolders(IEnumerable<string> folders) => Tidy(
        folders.Where(Directory.Exists)
            .SelectMany(f => SafeFiles(f, "*.desktop", SearchOption.AllDirectories))
            .Select(f => FromDesktopEntry(SafeRead(f)))
            .OfType<InstalledApp>());

    /// <summary>The places desktop entries live: the user's own first, then the system's and Flatpak's.</summary>
    public static IEnumerable<string> DesktopFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } d ? d : Path.Combine(home, ".local", "share");
        yield return Path.Combine(dataHome, "applications");
        var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } dirs ? dirs : "/usr/local/share:/usr/share";
        foreach (var dir in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries))
            yield return Path.Combine(dir, "applications");
        yield return "/var/lib/flatpak/exports/share/applications";
        yield return Path.Combine(dataHome, "flatpak", "exports", "share", "applications");
    }

    // ---- macOS: .app bundles ----

    /// <summary>The .app bundles in <paramref name="folders"/> (and one level of subfolders, like Utilities).</summary>
    public static IReadOnlyList<InstalledApp> FromAppFolders(IEnumerable<string> folders) => Tidy(
        folders.Where(Directory.Exists).SelectMany(f => SafeDirs(f, "*.app").Concat(SafeDirs(f, "*").Where(d => !d.EndsWith(".app", StringComparison.OrdinalIgnoreCase)).SelectMany(d => SafeDirs(d, "*.app"))))
            .Select(app => new InstalledApp(Path.GetFileNameWithoutExtension(app), app)));

    // ---- Windows: Start-menu shortcuts ----

    /// <summary>The Start menu's shortcuts that point to a program (.exe), named as the menu shows them.</summary>
    public static IReadOnlyList<InstalledApp> FromStartMenu(IEnumerable<string> folders, Func<string, string?> targetOf) => Tidy(
        folders.Where(Directory.Exists)
            .SelectMany(f => SafeFiles(f, "*.lnk", SearchOption.AllDirectories))
            .Select(link => (link, target: targetOf(link)))
            .Where(l => l.target is { } t && t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(l => new InstalledApp(Path.GetFileNameWithoutExtension(l.link), l.target!)));

    private static IEnumerable<string> SafeFiles(string folder, string pattern, SearchOption option)
    {
        try
        {
            return Directory.EnumerateFiles(folder, pattern, new EnumerationOptions
            {
                RecurseSubdirectories = option == SearchOption.AllDirectories,
                IgnoreInaccessible = true,
                MaxRecursionDepth = 4,
            }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeDirs(string folder, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(folder, pattern, new EnumerationOptions { IgnoreInaccessible = true }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string SafeRead(string path)
    {
        try
        {
            return new FileInfo(path).Length > 256 * 1024 ? "" : File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
