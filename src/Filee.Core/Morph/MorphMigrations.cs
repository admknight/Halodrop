// Morph change: Morph's own changes to an existing library (presets and donuts), numbered apart from Filee's settings
// schema (AppSettings.SchemaVersion). Filee's migrations keep their numbers when Morph merges a newer Filee; Morph's
// can never take one of them, so neither is skipped.

using Filee.Core.Presets;

namespace Filee.Core.Morph;

public static class MorphMigrations
{
    /// <summary>The newest of Morph's library changes.</summary>
    public const int Current = 1;

    /// <summary>Applies Morph's library changes after <paramref name="from"/>; true when something changed.</summary>
    public static bool Apply(int from, List<Preset> presets)
    {
        var changed = false;
        // 1: the file actions (copy path or name, show in folder, terminal, copy text, wallpaper) and their group, ready to
        //    put on a donut from the toolbar page. The donuts themselves stay as the user made them.
        if (from < 1)
        {
            foreach (var preset in BuiltInData.FileActionPresets())
            {
                if (presets.Any(p => p.Id == preset.Id))
                    continue;
                presets.Add(preset);
                changed = true;
            }
        }
        return changed;
    }
}
