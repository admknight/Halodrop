// Morph change: donut layout templates. A template is a whole set of donuts (both faces of each, their names and file
// types) and the donut's size. Two come built in and can't be changed: "Morph default", larger and with Morph's own
// groups placed, and "Filee classic", the layout Filee starts with. The user saves their own from the donuts they have,
// and switches between them in Settings → Toolbar; switching first keeps the current donuts as a template, so no layout
// is ever lost by trying another.

using System.Text.Json.Serialization;
using Filee.Core.Presets;
using Filee.Core.Profiles;

namespace Filee.Core.Morph;

public sealed class LayoutTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>The user's name for it (built-in templates are named by <see cref="NameKey"/>).</summary>
    public string Name { get; set; } = "";

    public string? NameKey { get; set; }

    /// <summary>The donut's outer radius with this layout (DonutSettings.OuterRadius).</summary>
    public double OuterRadius { get; set; } = LayoutTemplates.ClassicRadius;

    public List<ToolbarProfile> Profiles { get; set; } = [];

    /// <summary>When it was saved (the user's own templates).</summary>
    public DateTime? Saved { get; set; }

    /// <summary>A built-in template: shown, applied and copied, never changed or deleted.</summary>
    [JsonIgnore]
    public bool IsBuiltIn => Id is LayoutTemplates.MorphDefaultId or LayoutTemplates.FileeClassicId;

    public LayoutTemplate Copy() => new()
    {
        Id = Id,
        Name = Name,
        NameKey = NameKey,
        OuterRadius = OuterRadius,
        Saved = Saved,
        Profiles = [.. Profiles.Select(p => p.Clone())],
    };
}

public static class LayoutTemplates
{
    public const string MorphDefaultId = "morph-default";
    public const string FileeClassicId = "filee-classic";

    /// <summary>Filee's donut size.</summary>
    public const double ClassicRadius = 150;

    /// <summary>Morph's: larger, so a face with more slices keeps its labels readable.</summary>
    public const double MorphRadius = 180;

    /// <summary>The largest outer radius the donut takes (RadialViewModel and the size slider).</summary>
    public const double MaxRadius = 260;

    /// <summary>The built-in templates, Morph's first.</summary>
    public static IReadOnlyList<LayoutTemplate> BuiltIns() => [MorphDefault(), FileeClassic()];

    public static LayoutTemplate FileeClassic() => new()
    {
        Id = FileeClassicId,
        NameKey = "template.filee_classic",
        OuterRadius = ClassicRadius,
        Profiles = BuiltInData.CreateProfiles(),
    };

    /// <summary>
    /// Filee's donuts with Morph's groups placed where they're useful, up to the 12 slices a face holds: pictures get
    /// the social sizes and the picture actions in front, sound "Normalize volume", and every donut's back the file
    /// actions (copy path or name, show in folder, terminal).
    /// </summary>
    public static LayoutTemplate MorphDefault()
    {
        var profiles = BuiltInData.CreateProfiles();
        foreach (var profile in profiles)
        {
            switch (profile.Id)
            {
                case "images":
                    Add(profile.PresetIds, BuiltInData.SocialSizesId, BuiltInData.PictureActionsId);
                    break;
                case "audio":
                    Add(profile.PresetIds, "normalize-audio");
                    break;
            }
            Add(profile.BackPresetIds ??= [], BuiltInData.FileActionsId, "action-shelf");
        }
        return new() { Id = MorphDefaultId, NameKey = "template.morph_default", OuterRadius = MorphRadius, Profiles = profiles };
    }

    private static void Add(List<string> face, params string[] ids)
    {
        foreach (var id in ids)
        {
            if (!face.Contains(id) && face.Count < ToolbarProfile.MaxSlices)
                face.Add(id);
        }
    }

    /// <summary>
    /// Whether two sets of donuts are the same layout (names, file types, both faces in order), so switching from a
    /// layout already saved doesn't save it twice.
    /// </summary>
    public static bool SameLayout(IReadOnlyList<ToolbarProfile> a, IReadOnlyList<ToolbarProfile> b) =>
        a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Name == pair.Second.Name && pair.First.NameKey == pair.Second.NameKey
            && pair.First.IsFallback == pair.Second.IsFallback && pair.First.LabelShow == pair.Second.LabelShow
            && pair.First.LabelScale.Equals(pair.Second.LabelScale)
            && pair.First.Extensions.SequenceEqual(pair.Second.Extensions, StringComparer.OrdinalIgnoreCase)
            && pair.First.PresetIds.SequenceEqual(pair.Second.PresetIds)
            && (pair.First.BackPresetIds ?? []).SequenceEqual(pair.Second.BackPresetIds ?? []));
}
