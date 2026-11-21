// Morph change: the file actions and "Run a program" (ideas from OrbitDrop, MIT): placeholders, quoting of hostile
// file names, a real run through the shell, the migration that brings the actions to existing libraries, and the rule
// that a run action never arrives in an imported file.

using System.Text.Json.Nodes;
using Filee.Core.Presets;
using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class MorphFileActionTests
{
    [Fact]
    public void Placeholders_expand_once_and_quote_the_paths()
    {
        var run = new RunOptions { Program = "tool", Arguments = "-i {file} -d {folder} -n {name}.{ext} -- {files}" };
        var files = new[] { "/home/u/My Pics/shot {name}.png", "/home/u/b.jpg" };
        var line = run.Expand(files, windows: false);
        Assert.Equal("-i \"/home/u/My Pics/shot {name}.png\" -d \"/home/u/My Pics\" -n shot {name}.png -- \"/home/u/My Pics/shot {name}.png\" \"/home/u/b.jpg\"", line);
    }

    [Theory]
    [InlineData("/tmp/a$(touch x).png", "\"/tmp/a\\$(touch x).png\"")]
    [InlineData("/tmp/a`id`.png", "\"/tmp/a\\`id\\`.png\"")]
    [InlineData("/tmp/say \"hi\".png", "\"/tmp/say \\\"hi\\\".png\"")]
    [InlineData("/tmp/back\\slash.png", "\"/tmp/back\\\\slash.png\"")]
    public void Posix_quoting_keeps_shell_characters_literal(string path, string quoted) =>
        Assert.Equal(quoted, RunOptions.Quote(path, windows: false));

    [Fact]
    public void Windows_quoting_doubles_a_trailing_backslash() =>
        Assert.Equal("\"C:\\Photos\\\\\"", RunOptions.Quote("C:\\Photos\\", windows: true));

    [Fact]
    public void Windows_runs_an_exe_directly_and_anything_else_through_cmd()
    {
        var run = new RunOptions { Program = "code" };
        var direct = run.CommandLine("\"a.txt\"", windows: true, mac: false, _ => @"C:\Tools\code.exe");
        Assert.Equal(@"C:\Tools\code.exe", direct.FileName);
        Assert.Equal("\"a.txt\"", direct.Arguments);

        var script = run.CommandLine("\"a.txt\"", windows: true, mac: false, _ => @"C:\Bin\code.cmd");
        Assert.EndsWith("cmd.exe", script.FileName);
        Assert.Equal("/d /s /c \"\"C:\\Bin\\code.cmd\" \"a.txt\"\"", script.Arguments);

        run.ShowWindow = true;
        Assert.StartsWith("/d /s /k ", run.CommandLine("x", windows: true, mac: false, _ => @"C:\Tools\code.exe").Arguments);
    }

    [Fact]
    public void A_mac_app_opens_with_open_a()
    {
        var run = new RunOptions { Program = "/Applications/Preview.app" };
        var command = run.CommandLine("\"/tmp/a.png\"", windows: false, mac: true, _ => null);
        Assert.Equal("/bin/sh", command.FileName);
        Assert.Equal(["-c", "open -a \"/Applications/Preview.app\" \"/tmp/a.png\""], command.ArgumentList);
    }

    [Fact]
    public void A_hostile_file_name_reaches_the_program_whole_through_the_shell()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX shell.");
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "a $(touch pwned) `id` \"q\" & b.txt");
        File.WriteAllText(file, "x");
        var output = Path.Combine(dir.Path, "out.txt");
        var run = new RunOptions { Arguments = "printf '%s' {file} > " + RunOptions.Quote(output, false) };
        var command = run.CommandLine(run.Expand([file], false), windows: false, mac: false, _ => null);
        var info = new System.Diagnostics.ProcessStartInfo(command.FileName) { UseShellExecute = false, WorkingDirectory = dir.Path };
        foreach (var argument in command.ArgumentList)
            info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        process.WaitForExit(10_000);
        Assert.Equal(file, File.ReadAllText(output));
        Assert.False(File.Exists(Path.Combine(dir.Path, "pwned")));
    }

    [Theory]
    [InlineData("", "a.psd", true)]
    [InlineData("psd ai", "a.PSD", true)]
    [InlineData("*.psd, .ai", "b.ai", true)]
    [InlineData("psd", "c.png", false)]
    [InlineData("psd", "folder", false)]
    public void Extensions_limit_the_files(string extensions, string path, bool accepted) =>
        Assert.Equal(accepted, new RunOptions { Extensions = extensions }.Accepts(path));

    [Fact]
    public void An_action_with_nothing_to_run_says_so() =>
        Assert.Equal("run.problem_nothing", new RunOptions { Arguments = " " }.Problem());

    [Fact]
    public void A_new_library_has_the_file_actions_ready_for_a_donut()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.NotNull(store.FindPreset(BuiltInData.FileActionsId));
        Assert.All(BuiltInData.FileActionPresets().Where(p => p.IsTool), p => Assert.True(p.IsFileAction));
        Assert.All(store.Profiles, p => Assert.DoesNotContain(BuiltInData.FileActionsId, p.BackPresetIds ?? []));
        Assert.Equal(Morph.MorphMigrations.Current, store.Settings.MorphSchema);
    }

    [Fact]
    public void An_existing_library_gets_the_file_actions_once_and_its_donuts_stay_as_they_were()
    {
        using var dir = new TempDir();
        var first = new UserDataStore(dir.Path);
        first.Load();
        first.SaveSettings();
        first.SaveLibrary();

        // A library from before Morph's own migrations: no file actions, no Morph schema.
        var presetsPath = Path.Combine(dir.Path, "presets.json");
        var presets = JsonNode.Parse(File.ReadAllText(presetsPath))!.AsArray();
        foreach (var node in presets.Where(n => n!["id"]!.GetValue<string>() is var id && (id == BuiltInData.FileActionsId || id.StartsWith("action-", StringComparison.Ordinal))).ToList())
            presets.Remove(node);
        File.WriteAllText(presetsPath, presets.ToJsonString());
        var profilesPath = Path.Combine(dir.Path, "profiles.json");
        var profilesBefore = File.ReadAllText(profilesPath);
        var settingsPath = Path.Combine(dir.Path, "settings.json");
        var settings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
        settings.Remove("morphSchema");
        File.WriteAllText(settingsPath, settings.ToJsonString());

        var migrated = new UserDataStore(dir.Path);
        migrated.Load();
        Assert.NotNull(migrated.FindPreset("action-copy-path"));
        Assert.NotNull(migrated.FindPreset(BuiltInData.FileActionsId));
        migrated.SaveLibrary();
        Assert.Equal(profilesBefore, File.ReadAllText(profilesPath));

        // Deleted by the user, they stay deleted: the migration ran once.
        migrated.Presets.RemoveAll(p => p.Id == "action-copy-path");
        migrated.SaveLibrary();
        var again = new UserDataStore(dir.Path);
        again.Load();
        Assert.Null(again.FindPreset("action-copy-path"));
    }

    [Fact]
    public void A_library_at_Morph_step_1_gets_the_picture_actions_social_sizes_and_normalize()
    {
        using var dir = new TempDir();
        var first = new UserDataStore(dir.Path);
        first.Load();
        string[] step2 = ["action-palette", "action-reverse-search", BuiltInData.PictureActionsId, BuiltInData.SocialSizesId,
            "social-square", "social-story", "social-thumbnail", "normalize-audio"];
        first.Presets.RemoveAll(p => step2.Contains(p.Id));
        first.Settings.MorphSchema = 1;
        first.SaveSettings();
        first.SaveLibrary();

        var migrated = new UserDataStore(dir.Path);
        migrated.Load();
        Assert.All(step2, id => Assert.NotNull(migrated.FindPreset(id)));
        Assert.Equal(Filee.Core.Morph.MorphMigrations.Current, migrated.Settings.MorphSchema);
        Assert.All(migrated.Presets.Where(p => p.IsGroup), g => Assert.InRange(g.SubPresetIds.Count, 1, Preset.MaxSubPresets));
        Assert.Equal(migrated.Presets.Count, migrated.Presets.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Step_3_adds_share_and_print_to_the_built_in_group_while_it_has_room()
    {
        using var dir = new TempDir();
        var first = new UserDataStore(dir.Path);
        first.Load();
        first.Presets.RemoveAll(p => p.Id is "action-share" or "action-print");
        first.FindPreset(BuiltInData.FileActionsId)!.SubPresetIds.RemoveAll(id => id is "action-share" or "action-print");
        first.Settings.MorphSchema = 2;
        first.SaveSettings();
        first.SaveLibrary();

        var migrated = new UserDataStore(dir.Path);
        migrated.Load();
        Assert.NotNull(migrated.FindPreset("action-share"));
        Assert.NotNull(migrated.FindPreset("action-print"));
        Assert.Equal(["action-copy-path", "action-copy-name", "action-reveal", "action-terminal", "action-share", "action-print"],
            migrated.FindPreset(BuiltInData.FileActionsId)!.SubPresetIds);
        Assert.Equal(Filee.Core.Morph.MorphMigrations.Current, migrated.Settings.MorphSchema);
    }

    [Fact]
    public void Normalize_and_fill_survive_saving()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.SaveLibrary();
        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.True(reloaded.FindPreset("normalize-audio")!.Media.NormalizeLoudness);
        Assert.True(reloaded.FindPreset("social-story")!.Image.CustomSize!.Fill);
    }

    [Fact]
    public void An_imported_bundle_never_brings_a_program_to_run()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "mine-run", Name = "Mine", Kind = PresetKind.Tool, Tool = Preset.RunTool, Run = new RunOptions { Program = "calc" } });
        store.Presets.Add(new Preset { Id = "sneaky", Name = "Sneaky", TargetFormat = "png", Run = new RunOptions { Program = "calc" } });
        var bundle = Path.Combine(dir.Path, "bundle.json");
        store.Export(bundle);

        using var other = new TempDir();
        var receiver = new UserDataStore(other.Path);
        receiver.Load();
        receiver.Import(bundle);
        Assert.Null(receiver.FindPreset("mine-run"));
        Assert.Null(receiver.FindPreset("sneaky"));
    }

    [Fact]
    public void A_run_action_keeps_its_options_through_saving_and_cloning()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        var preset = new Preset
        {
            Id = "run-1",
            Name = "Open in editor",
            Kind = PresetKind.Tool,
            Tool = Preset.RunTool,
            TargetFormat = BuiltInData.SameAsSource,
            Run = new RunOptions { Program = "code", Arguments = "-r {files}", PerFile = true, ShowWindow = true, Extensions = "txt md" },
        };
        store.Presets.Add(preset);
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        var run = reloaded.FindPreset("run-1")!.Run!;
        Assert.Equal(("code", "-r {files}", true, true, "txt md"), (run.Program, run.Arguments, run.PerFile, run.ShowWindow, run.Extensions));

        var clone = preset.Clone();
        clone.Run!.Program = "other";
        Assert.Equal("code", preset.Run!.Program);
    }
}
