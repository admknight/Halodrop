// Morph change: the installed-app list for "Run a program": desktop entries, .app bundles and Start-menu shortcuts read
// the same way everywhere; hidden entries, uninstallers and field codes stay out.

using Filee.Core.Morph;
using Filee.Core.Platform;

namespace Filee.Core.Tests;

public class InstalledAppsTests
{
    [Fact]
    public void A_desktop_entry_gives_its_name_and_command_without_field_codes()
    {
        var app = InstalledApps.FromDesktopEntry("""
            [Desktop Entry]
            Type=Application
            Name=GNU Image Manipulation Program
            Name[de]=GIMP
            Exec=gimp-2.10 %U
            [Desktop Action new]
            Name=New Window
            Exec=gimp-2.10 --new
            """);
        Assert.Equal(new InstalledApp("GNU Image Manipulation Program", "gimp-2.10"), app);
        Assert.Equal("flatpak run org.gimp.GIMP", InstalledApps.StripFieldCodes("flatpak run org.gimp.GIMP @@u %U @@"));
    }

    [Theory]
    [InlineData("[Desktop Entry]\nType=Application\nName=X\nExec=x\nNoDisplay=true")]
    [InlineData("[Desktop Entry]\nType=Link\nName=X\nURL=https://example.com")]
    [InlineData("[Desktop Entry]\nType=Application\nName=X")]
    public void Hidden_links_and_entries_without_a_command_are_left_out(string text) =>
        Assert.Null(InstalledApps.FromDesktopEntry(text));

    [Fact]
    public void Uninstallers_and_doubles_are_left_out_and_the_rest_sorted()
    {
        var apps = InstalledApps.Tidy(
        [
            new("Photoshop", @"C:\Adobe\Photoshop.exe"), new("Uninstall Photoshop", @"C:\Adobe\uninst.exe"),
            new("code", "/usr/bin/code"), new("Code", "/snap/bin/code"), new("Blender", "/usr/bin/blender"), new(" ", "x"),
        ]);
        Assert.Equal(["Blender", "code", "Photoshop"], apps.Select(a => a.Name));
    }

    [Fact]
    public void Start_menu_shortcuts_to_programs_are_listed_by_their_name()
    {
        using var dir = new TempDir();
        var programs = Path.Combine(dir.Path, "Programs", "Adobe");
        Directory.CreateDirectory(programs);
        File.WriteAllText(Path.Combine(programs, "Adobe Photoshop 2026.lnk"), "");
        File.WriteAllText(Path.Combine(programs, "Read me.lnk"), "");
        var targets = new Dictionary<string, string>
        {
            ["Adobe Photoshop 2026.lnk"] = @"C:\Program Files\Adobe\Photoshop.exe",
            ["Read me.lnk"] = @"C:\Program Files\Adobe\readme.html",
        };
        var apps = InstalledApps.FromStartMenu([dir.Path, Path.Combine(dir.Path, "missing")], link => targets.GetValueOrDefault(Path.GetFileName(link)));
        Assert.Equal([new InstalledApp("Adobe Photoshop 2026", @"C:\Program Files\Adobe\Photoshop.exe")], apps);
    }

    [Fact]
    public void App_bundles_are_found_also_one_folder_down()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Preview.app", "Contents"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "Utilities", "Terminal.app"));
        var apps = InstalledApps.FromAppFolders([dir.Path]);
        Assert.Equal(["Preview", "Terminal"], apps.Select(a => a.Name));
        Assert.EndsWith("Terminal.app", apps[1].Program);
    }
}
