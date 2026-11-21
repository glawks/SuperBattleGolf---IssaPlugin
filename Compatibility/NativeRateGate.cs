using System;

namespace IssaPlugin.Compatibility
{
    // Sender-side pacing only. Native server validation is never patched.
    // Periodic quiet windows reset the native checker's cumulative violation count
    // even when transport jitter bunches a short burst at the server.
    internal sealed class NativeRateGate
    {
        private readonly double _interval, _quietWindow;
        private readonly int _burstLimit;
        private double _next = double.NegativeInfinity;
        private int _count;

        internal NativeRateGate(double interval, double quietWindow, int burstLimit = 4)
        {
            if (interval <= 0 || quietWindow <= interval || burstLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(interval));
            _interval = interval;
            _quietWindow = quietWindow;
            _burstLimit = burstLimit;
        }

        internal double Delay(double now) => Math.Max(0, _next - now);

        internal bool TryAcquire(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < _next)
                return false;
            _count++;
            if (_count >= _burstLimit)
            {
                _next = now + _quietWindow;
                _count = 0;
            }
            else _next = now + _interval;
            return true;
        }
    }
}
