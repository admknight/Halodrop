// Halodrop change: Halodrop was called Morph up to 0.7.0, which kept its settings in %APPDATA%\Morph and its downloaded
// engines in %LOCALAPPDATA%\Morph (~/Library/Application Support/Morph and ~/.local/share/Morph elsewhere). On the first
// start after the rename, those folders become Halodrop's, so nothing is set up or downloaded again.

namespace Filee.Core.Settings;

public static class MorphFolders
{
    /// <summary>
    /// Moves Morph's settings and engines folders to Halodrop's names when Halodrop has none yet. Does nothing with a
    /// data folder of its own (MORPH_DATA_DIR, portable mode) or when moving fails (it starts fresh then).
    /// </summary>
    public static void TakeOver(Action<string>? log = null)
    {
        if (UserDataStore.OverrideDirectory is not null)
            return;
        Move(Environment.SpecialFolder.ApplicationData, log);
        Move(Environment.SpecialFolder.LocalApplicationData, log);
    }

    internal static void Move(Environment.SpecialFolder root, Action<string>? log, string? rootPath = null)
    {
        var parent = rootPath ?? Environment.GetFolderPath(root);
        if (string.IsNullOrEmpty(parent))
            return;
        var from = Path.Combine(parent, "Morph");
        var to = Path.Combine(parent, "Halodrop");
        try
        {
            if (!Directory.Exists(from) || Directory.Exists(to) || File.Exists(to))
                return;
            Directory.Move(from, to);
            log?.Invoke($"Moved {from} to {to} (Morph is now Halodrop)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"Could not move {from} to {to}: {ex.Message}");
        }
    }
}
