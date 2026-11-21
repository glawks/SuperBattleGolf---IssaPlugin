using System;
using IssaPlugin.Compatibility;
using Xunit;

public class AutomaticFireClockTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void DefaultMinigunFiresContinuouslyAtFiftyShotsPerSecond(int fps)
    {
        var clock = new AutomaticFireClock(0, 0.4);
        int shots = 0;
        double lastShot = 0.4;
        for (int frame = 0; frame < fps * 5; frame++)
        {
            double now = (double)frame / fps;
            int count = clock.TakeDue(now, 0.02, 1000);
            if (now < 0.4) Assert.Equal(0, count);
            if (count > 0)
            {
                Assert.True(now - lastShot < 0.06); // no three/four-shot pauses
                lastShot = now;
            }
            shots += count;
        }
        Assert.InRange(shots, 228, 231);
    }

    [Fact]
    public void ServerStallCannotProduceAnUnboundedCatchUpBurst()
    {
        var clock = new AutomaticFireClock(0, 0);
        Assert.Equal(1, clock.TakeDue(0, 0.02, 200));
        Assert.Equal(4, clock.TakeDue(10, 0.02, 200));
        Assert.Equal(0, clock.TakeDue(10, 0.02, 200));
        Assert.Equal(1, clock.TakeDue(10.021, 0.02, 200));
    }

    [Fact]
    public void InvalidRatesAndEmptyAmmunitionDoNotGrantShots()
    {
        var clock = new AutomaticFireClock(0, 0);
        Assert.Equal(0, clock.TakeDue(1, double.NaN, 200));
        Assert.Equal(0, clock.TakeDue(1, -1, 200));
        Assert.Equal(0, clock.TakeDue(1, 0.02, 0));
        Assert.Equal(1, clock.TakeDue(1, 0.02, 1));
    }
}
