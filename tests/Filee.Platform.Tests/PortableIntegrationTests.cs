using System.Runtime.Versioning;
using Filee.Platform.Windows;
using Microsoft.Win32;

namespace Filee.Platform.Tests;

[SupportedOSPlatform("windows")]
public sealed class PortableIntegrationTests
{
    [Theory]
    [InlineData("\"C:\\Portable Morph\\Halodrop.exe\" --background", true)]
    [InlineData("\"C:\\Other\\Halodrop.exe\" --background", false)]
    [InlineData("\"C:\\Portable Morph\\Halodrop.exe.old\" --background", false)]
    [InlineData("\"C:\\Other\\launcher.exe\" \"C:\\Portable Morph\\Halodrop.exe\"", false)]
    [InlineData("Halodrop.exe --background", false)]
    [InlineData("\"C:\\Portable Morph\\Halodrop.exe", false)]
    public void Ownership_requires_the_exact_executable_as_the_command_target(string command, bool expected)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows paths.");
        Assert.Equal(expected, PortableIntegration.CommandTargets(command, @"C:\Portable Morph\Halodrop.exe"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Cleanup_removes_only_owned_entries_in_an_isolated_registry_tree(bool ownedRun, bool ownedVerb)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows registry.");
        var path = @"Software\Morph.PortableTests\" + Guid.NewGuid().ToString("N");
        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(path);
            using var run = root.CreateSubKey(PortableIntegration.RunKey);
            using var command = root.CreateSubKey(PortableIntegration.VerbKey + @"\command");
            var executable = @"C:\Portable Morph\Halodrop.exe";
            var target = ownedRun ? executable : @"C:\Installed\Halodrop.exe";
            var verbTarget = ownedVerb ? executable : @"C:\Installed\Halodrop.exe";
            run.SetValue("Halodrop", $"\"{target}\" --background");
            run.SetValue("OtherApp", "keep");
            command.SetValue(null, $"\"{verbTarget}\" --convert \"%1\"");
            command.Close();
            Assert.Equal(ownedRun || ownedVerb, PortableIntegration.HasRegistryEntries(root, executable));
            PortableIntegration.RemoveRegistryEntries(root, executable);
            Assert.Equal(ownedRun ? null : $"\"{target}\" --background", run.GetValue("Halodrop"));
            Assert.Equal("keep", run.GetValue("OtherApp"));
            using var remaining = root.OpenSubKey(PortableIntegration.VerbKey);
            Assert.Equal(!ownedVerb, remaining is not null);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }
}
