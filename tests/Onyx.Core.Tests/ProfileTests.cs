using Onyx.Core.Settings;
using Xunit;

namespace Onyx.Core.Tests;

public class ProfileTests
{
    // MainWindow indexes slots 0..2 directly off this list.
    [Fact]
    public void DefaultsAreExactlyThreeNamedSlots()
    {
        var p = OnyxSettings.DefaultProfiles();
        Assert.Equal(3, p.Count);
        Assert.All(p, x => Assert.False(string.IsNullOrWhiteSpace(x.Name)));
        Assert.Equal(3, p.Select(x => x.Name).Distinct().Count());
    }

    [Fact]
    public void DefaultsStayInsideTheRangesTheUiClampsTo()
    {
        foreach (var p in OnyxSettings.DefaultProfiles())
        {
            Assert.InRange(p.CoverStyle, 0, 5);
            Assert.InRange(p.MosaicBlockSize, 4, 60);
            Assert.InRange(p.DetectEveryN, 1, 8);
            Assert.InRange(p.ScoreThreshold, 0.30, 0.90);
            Assert.InRange(p.LatchFrames, 0, 120);
            Assert.InRange(p.BackgroundMode, 0, 3);
            Assert.InRange(p.BackgroundStrength, 1, 30);
            Assert.InRange(p.OutputEffect, 0, 2);
        }
    }

    // Paranoid mode is the fail-safe that covers the whole frame when no face is
    // found. No stock profile should ship with it off.
    [Fact]
    public void EveryDefaultProfileKeepsTheFailSafeOn()
    {
        Assert.All(OnyxSettings.DefaultProfiles(), p => Assert.True(p.ParanoidMode));
    }
}
