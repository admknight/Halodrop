// Morph change: donut layout templates. The built-in ones fit the donut's limits and name only presets that exist;
// switching keeps the current donuts as a template first (never twice), and the user's own survive a restart.

using Filee.Core.Morph;
using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class LayoutTemplateTests
{
    [Fact]
    public void Morph_default_is_bigger_has_more_slices_and_stays_within_the_limits()
    {
        var morph = LayoutTemplates.MorphDefault();
        var classic = LayoutTemplates.Classic();
        var ids = BuiltInData.CreatePresets().Select(p => p.Id).ToHashSet();

        Assert.True(morph.OuterRadius > classic.OuterRadius);
        Assert.True(Slices(morph) > Slices(classic));
        Assert.All(morph.Profiles, p =>
        {
            Assert.InRange(p.PresetIds.Count, 0, ToolbarProfile.FileeMaxSlices);
            Assert.InRange(p.BackPresetIds!.Count, 1, ToolbarProfile.FileeMaxSlices);
            Assert.All(p.PresetIds.Concat(p.BackPresetIds), id => Assert.Contains(id, ids));
            Assert.Equal(p.PresetIds.Count, p.PresetIds.Distinct().Count());
        });
        var images = morph.Profiles.Single(p => p.Id == "images");
        Assert.Contains(BuiltInData.SocialSizesId, images.PresetIds);
        Assert.Contains(BuiltInData.PictureActionsId, images.PresetIds);
        Assert.Contains("normalize-audio", morph.Profiles.Single(p => p.Id == "audio").PresetIds);
        Assert.True(LayoutTemplates.SameLayout(classic.Profiles, BuiltInData.CreateProfiles()));
        Assert.All(LayoutTemplates.BuiltIns(), t => Assert.True(t.IsBuiltIn));
    }

    [Fact]
    public void Switching_keeps_the_current_donuts_once_and_sets_the_size()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Profiles[0].PresetIds.Reverse(); // the user's own arrangement
        var mine = store.Profiles.Select(p => p.Clone()).ToList();

        var kept = store.ApplyTemplate(LayoutTemplates.MorphDefault(), "Mine");
        Assert.NotNull(kept);
        Assert.Equal("Mine", kept.Name);
        Assert.True(LayoutTemplates.SameLayout(mine, kept.Profiles));
        Assert.True(LayoutTemplates.SameLayout(LayoutTemplates.MorphDefault().Profiles, store.Profiles));
        Assert.Equal(LayoutTemplates.MorphRadius, store.Settings.Donut.OuterRadius);

        // Back to the user's: Morph default is built in, so nothing new is kept.
        Assert.Null(store.ApplyTemplate(kept, "Again"));
        Assert.True(LayoutTemplates.SameLayout(mine, store.Profiles));
        Assert.Single(store.OwnTemplates);

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.False(reloaded.IsNewLibrary);
        Assert.True(LayoutTemplates.SameLayout(mine, reloaded.Profiles));
        var own = Assert.Single(reloaded.OwnTemplates);
        Assert.Equal("Mine", own.Name);
        Assert.Equal(3, reloaded.Templates.Count);
    }

    [Fact]
    public void A_template_brings_back_built_in_presets_the_user_deleted()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.RemoveAll(p => p.Id is "normalize-audio" or BuiltInData.SocialSizesId);
        store.SaveLibrary();
        store.ApplyTemplate(LayoutTemplates.MorphDefault(), "Before");
        Assert.NotNull(store.FindPreset("normalize-audio"));
        Assert.NotNull(store.FindPreset(BuiltInData.SocialSizesId));
    }

    [Fact]
    public void Saved_and_deleted_templates_are_the_users_own_only()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.True(store.IsNewLibrary);
        var saved = store.SaveCurrentAsTemplate("  Work  ");
        Assert.Equal("Work", saved.Name);
        Assert.False(saved.IsBuiltIn);
        Assert.False(store.DeleteTemplate(LayoutTemplates.MorphDefaultId));
        Assert.True(store.DeleteTemplate(saved.Id));
        Assert.Empty(store.OwnTemplates);
    }

    private static int Slices(LayoutTemplate t) => t.Profiles.Sum(p => p.PresetIds.Count + (p.BackPresetIds?.Count ?? 0));
}
