// Morph change: Morph's own changes to an existing library (presets and donuts), numbered apart from Filee's settings
// schema (AppSettings.SchemaVersion). Filee's migrations keep their numbers when Morph merges a newer Filee; Morph's
// can never take one of them, so neither is skipped.

using Filee.Core.Presets;

namespace Filee.Core.Morph;

public static class MorphMigrations
{
    /// <summary>The newest of Morph's library changes.</summary>
    public const int Current = 3;

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
        // 2: colour palette and search the web for a picture (with copy text and wallpaper in a "Picture actions" group),
        //    the social sizes and "Normalize volume".
        if (from < 2)
        {
            foreach (var preset in BuiltInData.FileActionPresets().Where(p => p.Id != BuiltInData.FileActionsId)
                         .Concat(BuiltInData.SocialPresets()).Append(BuiltInData.NormalizeAudioPreset()))
            {
                if (presets.Any(p => p.Id == preset.Id))
                    continue;
                presets.Add(preset);
                changed = true;
            }
        }
        // 3: share and print, which join the built-in "File actions" group while it has room.
        if (from < 3)
        {
            var all = BuiltInData.FileActionPresets();
            foreach (var preset in new[] { "action-share", "action-print" }.Select(id => all.Single(p => p.Id == id)))
            {
                if (presets.Any(p => p.Id == preset.Id))
                    continue;
                presets.Add(preset);
                changed = true;
                if (presets.FirstOrDefault(p => p.Id == BuiltInData.FileActionsId) is { } group && group.SubPresetIds.Count < Preset.MaxSubPresets
                    && !group.SubPresetIds.Contains(preset.Id))
                    group.SubPresetIds.Add(preset.Id);
            }
        }
        return changed;
    }
}
