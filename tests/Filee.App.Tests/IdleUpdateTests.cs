// Morph change: an update installs by itself only after Morph has been idle for the whole wait, never while it's used,
// never a required one (which says why it's needed), and not when the setting is off.

using Filee.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.App.Tests;

public class IdleUpdateTests
{
    private static UpdateService Updates(string? latest = "9.9.9") =>
        new(NullLogger<UpdateService>.Instance) { LatestVersion = latest };

    [Fact]
    public void It_installs_only_after_being_idle_for_the_whole_wait()
    {
        var updates = Updates();
        var start = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(updates.ShouldInstallNow(true, false, start));
        Assert.False(updates.ShouldInstallNow(true, false, start + UpdateService.IdleBeforeInstall - TimeSpan.FromSeconds(1)));
        Assert.True(updates.ShouldInstallNow(true, false, start + UpdateService.IdleBeforeInstall));
    }

    [Fact]
    public void Using_Morph_starts_the_wait_again()
    {
        var updates = Updates();
        var start = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
        updates.ShouldInstallNow(true, false, start);
        Assert.False(updates.ShouldInstallNow(true, true, start + TimeSpan.FromMinutes(9))); // a conversion, the donut
        Assert.False(updates.ShouldInstallNow(true, false, start + TimeSpan.FromMinutes(11)));
        Assert.True(updates.ShouldInstallNow(true, false, start + TimeSpan.FromMinutes(21)));
    }

    [Fact]
    public void Nothing_installs_with_the_setting_off_or_no_update()
    {
        var start = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
        var off = Updates();
        off.ShouldInstallNow(false, false, start);
        Assert.False(off.ShouldInstallNow(false, false, start + TimeSpan.FromHours(1)));
        var none = Updates(null);
        none.ShouldInstallNow(true, false, start);
        Assert.False(none.ShouldInstallNow(true, false, start + TimeSpan.FromHours(1)));
    }
}
