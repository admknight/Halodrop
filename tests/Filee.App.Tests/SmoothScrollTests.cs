// Smooth mouse-wheel scrolling (SmoothScroll): which wheel deltas count as notches, how far a spin carries, and
// how the page follows its target. The wheel handling itself needs a pointer device, so what is tested here is the
// arithmetic. Halodrop change: they run on the headless UI thread, like TickProgressRingTests: SmoothScroll's static
// constructor registers an Avalonia property on ScrollViewer, and doing that on another thread before the headless
// session starts left every UI test without a UI thread (2,531 failures on the Linux x64 package run).

using Avalonia.Headless.XUnit;
using Filee.App.Controls;

namespace Filee.App.Tests;

public class SmoothScrollTests
{
    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(3.0)]
    [InlineData(-2.0)]
    public void A_whole_wheel_notch_is_a_notch(double delta) => Assert.True(SmoothScroll.IsWholeNotch(delta));

    [AvaloniaTheory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(-0.33)]
    [InlineData(0.5)]
    [InlineData(1.4)]
    public void A_touchpad_or_smooth_wheel_step_is_not(double delta) => Assert.False(SmoothScroll.IsWholeNotch(delta));

    [AvaloniaFact]
    public void A_spin_scrolls_further_per_notch_up_to_a_limit()
    {
        Assert.Equal(1, SmoothScroll.Boost(0));
        Assert.True(SmoothScroll.Boost(3) > SmoothScroll.Boost(1));
        Assert.Equal(SmoothScroll.Boost(50), SmoothScroll.Boost(500));
        Assert.True(SmoothScroll.Boost(500) <= 4);
    }

    [AvaloniaFact]
    public void From_rest_the_page_moves_toward_the_target_without_passing_it()
    {
        var (position, velocity) = (0.0, 0.0);
        var previous = position;
        for (var i = 0; i < 300; i++)
        {
            (position, velocity) = SmoothScroll.Follow(position, velocity, 200, 0.008);
            Assert.InRange(position, previous, 200);
            previous = position;
        }
        Assert.InRange(position, 199.9, 200);
        Assert.InRange(velocity, -1, 1);
    }

    [AvaloniaFact]
    public void The_page_starts_gently_and_speeds_up()
    {
        // The speed grows from nothing: no jump at the first frame.
        var (_, first) = SmoothScroll.Follow(0, 0, 200, 0.008);
        var (_, later) = SmoothScroll.Follow(0, 0, 200, 0.08);
        Assert.True(first > 0);
        Assert.True(later > first);
    }

    [AvaloniaFact]
    public void Downwards_works_too()
    {
        var (position, _) = SmoothScroll.Follow(500, 0, 100, 0.016);
        Assert.InRange(position, 100, 500);
        Assert.True(position < 500);
    }

    [AvaloniaFact]
    public void No_time_passed_means_no_change()
    {
        var (position, velocity) = SmoothScroll.Follow(40, 12, 200, 0);
        Assert.Equal(40, position);
        Assert.Equal(12, velocity);
    }

    [AvaloniaFact]
    public void The_speed_it_has_is_kept_when_the_target_moves()
    {
        // Moving at speed and a new target ahead of it: it goes on in the same direction, not back to a stop.
        var (_, velocity) = SmoothScroll.Follow(50, 300, 400, 0.008);
        Assert.True(velocity > 0);
    }

    [AvaloniaFact]
    public void However_the_time_is_split_into_frames_the_page_is_in_the_same_place()
    {
        var (onePosition, oneVelocity) = SmoothScroll.Follow(0, 0, 100, 0.064);
        var (position, velocity) = (0.0, 0.0);
        for (var i = 0; i < 8; i++)
            (position, velocity) = SmoothScroll.Follow(position, velocity, 100, 0.008);
        Assert.Equal(onePosition, position, 6);
        Assert.Equal(oneVelocity, velocity, 6);
    }
}
