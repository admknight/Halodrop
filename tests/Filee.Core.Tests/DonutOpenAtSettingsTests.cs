// Morph change: where a drag's donut opens (Settings → Shortcuts → Where the donut opens), in settings.json.

using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class DonutOpenAtSettingsTests
{
    private static UserDataStore NewStore(TempDir dir)
    {
        var store = new UserDataStore(dir.Path);
        store.Load();
        return store;
    }

    [Fact]
    public void New_settings_open_the_donut_where_the_files_are_let_go()
    {
        using var dir = new TempDir();
        Assert.Equal(DonutOpenAt.LetGo, NewStore(dir).Settings.Donut.OpenAt);
    }

    [Fact]
    public void Settings_from_before_it_take_the_default_and_a_choice_is_saved_by_name()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"),
            $$"""{ "schemaVersion": {{AppSettings.CurrentSchemaVersion}}, "donut": { "outerRadius": 180 } }""");
        var store = NewStore(dir);
        Assert.Equal(DonutOpenAt.LetGo, store.Settings.Donut.OpenAt);

        store.Settings.Donut.OpenAt = DonutOpenAt.FollowMe;
        store.SaveSettings();
        Assert.Contains("\"FollowMe\"", File.ReadAllText(Path.Combine(dir.Path, "settings.json")));
        Assert.Equal(DonutOpenAt.FollowMe, NewStore(dir).Settings.Donut.OpenAt);
        Assert.Equal(180, NewStore(dir).Settings.Donut.OuterRadius);
    }
}
