using System;
using System.Collections.Generic;
using IssaPlugin.Compatibility;
using Xunit;

public class NativeRateGateTests
{
    [Theory]
    [InlineData(0.15, 1.25, 0.1, 1.0)] // ammo, including rapid weapon switches
    [InlineData(0.65, 2.25, 0.5, 2.0)] // each target's hit limiter
    [InlineData(0.35, 2.25, 0.25, 2.0)] // native hit/miss VFX, multiple targets
    [InlineData(0.3, 1.25, 0.2, 1.0)] // shot audio
    public void SustainedRequestsAndBunchedDeliveryStayBelowSuspicion(
        double interval, double quiet, double nativeMinimum, double nativeReset)
    {
        var gate = new NativeRateGate(interval, quiet);
        var times = new List<double>();
        // User configuration asks for 50Hz. The source gate controls emitted calls.
        for (int frame = 0; frame < 30000; frame++)
        {
            double time = frame * 0.02;
            if (gate.TryAcquire(time)) times.Add(time);
        }
        Assert.True(times.Count > 100);
        double previous = double.NegativeInfinity;
        int violations = 0;
        // Adverse reliable delivery: each four-message burst arrives bunched up
        // with 200ms extra latency. The quiet interval must still reset the checker.
        for (int first = 0; first < times.Count; first += 4)
        {
            int last = Math.Min(first + 3, times.Count - 1);
            double arrival = times[last] + 0.2;
            for (int i = first; i <= last; i++)
            {
                double gap = arrival - previous;
                if (gap > nativeReset) violations = 0;
                if (gap <= nativeMinimum) violations++;
                Assert.True(violations < 5, $"Native suspicious threshold reached at {arrival}.");
                previous = arrival;
            }
        }
    }

    [Fact]
    public void RejectedAttemptsDoNotExtendCooldownOrAcceptInvalidClock()
    {
        var gate = new NativeRateGate(0.15, 1.25);
        Assert.True(gate.TryAcquire(0));
        Assert.False(gate.TryAcquire(0.01));
        Assert.False(gate.TryAcquire(double.NaN));
        Assert.False(gate.TryAcquire(double.PositiveInfinity));
        Assert.True(gate.TryAcquire(0.16));
    }
}
